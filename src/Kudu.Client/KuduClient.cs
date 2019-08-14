﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Client.Builder;
using Kudu.Client.Connection;
using Kudu.Client.Exceptions;
using Kudu.Client.Internal;
using Kudu.Client.Protocol.Consensus;
using Kudu.Client.Protocol.Master;
using Kudu.Client.Protocol.Rpc;
using Kudu.Client.Protocol.Tserver;
using Kudu.Client.Requests;
using Kudu.Client.Tablet;
using Kudu.Client.Util;

namespace Kudu.Client
{
    public class KuduClient : IAsyncDisposable
    {
        /// <summary>
        /// The number of tablets to fetch from the master in a round trip when
        /// performing a lookup of a single partition (e.g. for a write), or
        /// re-looking-up a tablet with stale information.
        /// </summary>
        private const int FetchTabletsPerPointLookup = 10;

        private readonly KuduClientOptions _options;
        private readonly IKuduConnectionFactory _connectionFactory;
        private readonly ConnectionCache _connectionCache;
        private readonly Dictionary<string, TableLocationsCache> _tableLocations;

        private volatile ServerInfoCache _masterCache;
        private volatile string _location;

        public KuduClient(KuduClientOptions options)
        {
            _options = options;
            _connectionFactory = new KuduConnectionFactory();
            _connectionCache = new ConnectionCache(_connectionFactory);
            _tableLocations = new Dictionary<string, TableLocationsCache>();
        }

        public async Task<KuduTable> CreateTableAsync(TableBuilder table)
        {
            var rpc = new CreateTableRequest(table.Build());

            await SendRpcToMasterAsync(rpc).ConfigureAwait(false);
            var result = rpc.Response;

            // TODO: Handle errors elsewhere?
            if (result.Error != null)
                throw new MasterException(result.Error);

            await WaitForTableDoneAsync(result.TableId).ConfigureAwait(false);

            var tableIdentifier = new TableIdentifierPB { TableId = result.TableId };
            return await OpenTableAsync(tableIdentifier).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a table on the cluster with the specified name.
        /// </summary>
        /// <param name="tableName">The table's name.</param>
        /// <param name="modifyExternalCatalogs">Whether to apply the deletion to external catalogs, such as the Hive Metastore.</param>
        public async Task DeleteTableAsync(string tableName, bool modifyExternalCatalogs = true)
        {
            var rpc = new DeleteTableRequest(new DeleteTableRequestPB
            {
                Table = new TableIdentifierPB { TableName = tableName },
                ModifyExternalCatalogs = modifyExternalCatalogs
            });

            await SendRpcToMasterAsync(rpc).ConfigureAwait(false);
            var result = rpc.Response;

            // TODO: Handle errors elsewhere?
            if (result.Error != null)
                throw new MasterException(result.Error);
        }

        public async Task<List<ListTablesResponsePB.TableInfo>> GetTablesAsync(string nameFilter = null)
        {
            var rpc = new ListTablesRequest(new ListTablesRequestPB
            {
                NameFilter = nameFilter
            });

            await SendRpcToMasterAsync(rpc).ConfigureAwait(false);
            var result = rpc.Response;

            // TODO: Handle errors elsewhere?
            if (result.Error != null)
                throw new MasterException(result.Error);

            return result.Tables;
        }

        public async Task<List<RemoteTablet>> GetTableLocationsAsync(
            string tableId, byte[] partitionKey, uint fetchBatchSize)
        {
            // TODO: rate-limit master lookups.

            var rpc = new GetTableLocationsRequest(new GetTableLocationsRequestPB
            {
                Table = new TableIdentifierPB { TableId = tableId.ToUtf8ByteArray() },
                PartitionKeyStart = partitionKey,
                MaxReturnedLocations = fetchBatchSize
            });

            await SendRpcToMasterAsync(rpc).ConfigureAwait(false);
            var result = rpc.Response;

            if (result.Error != null)
                throw new MasterException(result.Error);

            var tabletLocations = new List<RemoteTablet>(result.TabletLocations.Count);

            foreach (var tabletLocation in result.TabletLocations)
            {
                var tablet = await _connectionFactory.GetTabletAsync(tableId, tabletLocation)
                    .ConfigureAwait(false);
                tabletLocations.Add(tablet);
            }

            return tabletLocations;
        }

        public Task<GetTableSchemaResponsePB> GetTableSchemaAsync(string tableName)
        {
            var tableIdentifier = new TableIdentifierPB { TableName = tableName };
            return GetTableSchemaAsync(tableIdentifier);
        }

        public async Task<KuduTable> OpenTableAsync(string tableName)
        {
            var tableIdentifier = new TableIdentifierPB { TableName = tableName };
            var response = await GetTableSchemaAsync(tableIdentifier).ConfigureAwait(false);

            return new KuduTable(response);
        }

        public async Task<WriteResponsePB> WriteRowAsync(Operation operation)
        {
            var row = operation.Row;
            var table = operation.Table;
            var rows = new byte[row.RowSize];
            var indirectData = new byte[row.IndirectDataSize];

            row.WriteTo(rows, indirectData);

            var rowOperations = new Protocol.RowOperationsPB
            {
                Rows = rows,
                IndirectData = indirectData
            };

            var tablet = await GetRowTabletAsync(table, row).ConfigureAwait(false);
            if (tablet == null)
                throw new Exception("The requested tablet does not exist");

            var server = GetServerInfo(tablet, ReplicaSelection.LeaderOnly);
            var connection = await _connectionCache.GetConnectionAsync(server).ConfigureAwait(false);

            var rpc = new WriteRequest(new WriteRequestPB
            {
                TabletId = tablet.TabletId.ToUtf8ByteArray(),
                Schema = table.SchemaPb.Schema,
                RowOperations = rowOperations
            });

            await SendRpcToConnectionAsync(rpc, connection).ConfigureAwait(false);
            var result = rpc.Response;

            if (result.Error != null)
                throw new TabletServerException(result.Error);

            return result;
        }

        public async Task<WriteResponsePB[]> WriteRowAsync(IEnumerable<Operation> operations)
        {
            var operationsByTablet = new Dictionary<RemoteTablet, List<Operation>>();

            foreach (var operation in operations)
            {
                var tablet = await GetRowTabletAsync(operation.Table, operation.Row)
                    .ConfigureAwait(false);

                if (tablet != null)
                {
                    if (!operationsByTablet.TryGetValue(tablet, out var tabletOperations))
                    {
                        tabletOperations = new List<Operation>();
                        operationsByTablet.Add(tablet, tabletOperations);
                    }

                    tabletOperations.Add(operation);
                }
                else
                {
                    // TODO: Handle failure
                    Console.WriteLine("Unable to find tablet");
                }
            }

            var tasks = new Task<WriteResponsePB>[operationsByTablet.Count];
            var i = 0;

            foreach (var tabletOperations in operationsByTablet)
            {
                var task = WriteRowAsync(tabletOperations.Value, tabletOperations.Key);
                tasks[i++] = task;
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            return results;
        }

        private async Task<WriteResponsePB> WriteRowAsync(List<Operation> operations, RemoteTablet tablet)
        {
            var table = operations[0].Table;

            byte[] rowData;
            byte[] indirectData;

            // TODO: Estimate better sizes for these.
            using (var rowBuffer = new BufferWriter(1024))
            using (var indirectBuffer = new BufferWriter(1024))
            {
                OperationsEncoder.Encode(operations, rowBuffer, indirectBuffer);

                // protobuf-net doesn't support serializing Memory<byte>,
                // so we need to copy these into an array.
                rowData = rowBuffer.Memory.ToArray();
                indirectData = indirectBuffer.Memory.ToArray();
            }

            var rowOperations = new Protocol.RowOperationsPB
            {
                Rows = rowData,
                IndirectData = indirectData
            };

            var server = GetServerInfo(tablet, ReplicaSelection.LeaderOnly);
            var connection = await _connectionCache.GetConnectionAsync(server).ConfigureAwait(false);

            var rpc = new WriteRequest(new WriteRequestPB
            {
                TabletId = tablet.TabletId.ToUtf8ByteArray(),
                Schema = table.SchemaPb.Schema,
                RowOperations = rowOperations
            });

            await SendRpcToConnectionAsync(rpc, connection).ConfigureAwait(false);
            var result = rpc.Response;

            if (result.Error != null)
                throw new TabletServerException(result.Error);

            return result;
        }

        public ScanBuilder NewScanBuilder(KuduTable table)
        {
            return new ScanBuilder(this, table);
        }

        public async Task<KuduTable> OpenTableAsync(TableIdentifierPB tableIdentifier)
        {
            var response = await GetTableSchemaAsync(tableIdentifier).ConfigureAwait(false);

            return new KuduTable(response);
        }

        private async Task<GetTableSchemaResponsePB> GetTableSchemaAsync(TableIdentifierPB tableIdentifier)
        {
            var rpc = new GetTableSchemaRequest(new GetTableSchemaRequestPB
            {
                Table = tableIdentifier
            });

            await SendRpcToMasterAsync(rpc).ConfigureAwait(false);
            var result = rpc.Response;

            if (result.Error != null)
                throw new MasterException(result.Error);

            return result;
        }

        private async Task WaitForTableDoneAsync(byte[] tableId)
        {
            var rpc = new IsCreateTableDoneRequest(new IsCreateTableDoneRequestPB
            {
                Table = new TableIdentifierPB { TableId = tableId }
            });

            while (true)
            {
                await SendRpcToMasterAsync(rpc).ConfigureAwait(false);
                var result = rpc.Response;

                if (result.Error != null)
                    throw new MasterException(result.Error);

                if (result.Done)
                    break;

                await Task.Delay(50).ConfigureAwait(false);
                // TODO: Increment rpc attempts.
            }
        }

        private async Task ConnectToClusterAsync()
        {
            var masters = new List<ServerInfo>(_options.MasterAddresses.Count);
            int leaderIndex = -1;
            string location = null;
            foreach (var master in _options.MasterAddresses)
            {
                var serverInfo = await _connectionFactory.GetServerInfoAsync(
                    "master", location: null, master).ConfigureAwait(false);
                var connection = await _connectionCache.GetConnectionAsync(serverInfo).ConfigureAwait(false);
                var rpc = new ConnectToMasterRequest();
                await SendRpcToConnectionAsync(rpc, connection).ConfigureAwait(false);
                var response = rpc.Response;

                if (response.Role == RaftPeerPB.Role.Leader)
                {
                    leaderIndex = masters.Count;
                }

                location = response.ClientLocation;
                masters.Add(serverInfo);
            }

            if (leaderIndex == -1)
                throw new Exception("Unable to find master leader");

            _masterCache = new ServerInfoCache(masters, leaderIndex);
            _location = location;
        }

        private async Task SendRpcToMasterAsync(KuduRpc rpc)
        {
            // TODO: Don't allow this to happen in parallel.
            if (_masterCache == null)
                await ConnectToClusterAsync().ConfigureAwait(false);

            var master = GetMasterServerInfo(rpc.ReplicaSelection);
            var connection = await _connectionCache.GetConnectionAsync(master).ConfigureAwait(false);

            await SendRpcToConnectionAsync(rpc, connection).ConfigureAwait(false);
        }

        private async Task SendRpcToConnectionAsync(KuduRpc rpc, KuduConnection connection)
        {
            var header = new RequestHeader
            {
                // CallId is set by KuduConnection.
                RemoteMethod = new RemoteMethodPB
                {
                    ServiceName = rpc.ServiceName,
                    MethodName = rpc.MethodName
                }
                // TODO: Set RequiredFeatureFlags
            };

            await connection.SendReceiveAsync(header, rpc).ConfigureAwait(false);
        }

        private ServerInfo GetServerInfo(RemoteTablet tablet, ReplicaSelection replicaSelection)
        {
            return tablet.GetServerInfo(replicaSelection, _location);
        }

        private ServerInfo GetMasterServerInfo(ReplicaSelection replicaSelection)
        {
            return _masterCache.GetServerInfo(replicaSelection, _location);
        }

        internal ValueTask<RemoteTablet> GetRowTabletAsync(KuduTable table, PartialRow row)
        {
            using (var writer = new BufferWriter(256))
            {
                KeyEncoder.EncodePartitionKey(row, table.PartitionSchema, writer);
                var partitionKey = writer.Memory.Span;

                // Note that we don't have to await this method before disposing the writer, as a
                // copy of partitionKey will be made if the method cannot complete synchronously.
                return GetTabletAsync(table.TableId, partitionKey);
            }
        }

        /// <summary>
        /// Locates a tablet by consulting the table location cache, then by contacting
        /// a master if we haven't seen the tablet before. The results are cached.
        /// </summary>
        /// <param name="tableId">The table identifier.</param>
        /// <param name="partitionKey">The partition key.</param>
        /// <returns>The requested tablet, or null if the tablet doesn't exist.</returns>
        private ValueTask<RemoteTablet> GetTabletAsync(string tableId, ReadOnlySpan<byte> partitionKey)
        {
            var tablet = GetTabletFromCache(tableId, partitionKey);

            if (tablet != null)
                return new ValueTask<RemoteTablet>(tablet);

            var task = LookupAndCacheTabletAsync(tableId, partitionKey.ToArray());
            return new ValueTask<RemoteTablet>(task);
        }

        /// <summary>
        /// Locates a tablet by consulting the table location cache.
        /// </summary>
        /// <param name="tableId">The table identifier.</param>
        /// <param name="partitionKey">The partition key.</param>
        /// <returns>The requested tablet, or null if the tablet doesn't exist.</returns>
        private RemoteTablet GetTabletFromCache(string tableId, ReadOnlySpan<byte> partitionKey)
        {
            TableLocationsCache tableCache;

            lock (_tableLocations)
            {
                if (!_tableLocations.TryGetValue(tableId, out tableCache))
                {
                    // We don't have any tablets cached for this table.
                    return null;
                }
            }

            return tableCache.FindTablet(partitionKey);
        }

        /// <summary>
        /// Locates a tablet by consulting a master and caches the results.
        /// </summary>
        /// <param name="tableId">The table identifier.</param>
        /// <param name="partitionKey">The partition key.</param>
        /// <returns>The requested tablet, or null if the tablet doesn't exist.</returns>
        private async Task<RemoteTablet> LookupAndCacheTabletAsync(string tableId, byte[] partitionKey)
        {
            var tablets = await GetTableLocationsAsync(
                tableId, partitionKey, FetchTabletsPerPointLookup).ConfigureAwait(false);

            CacheTablets(tableId, tablets, partitionKey);

            var tablet = GetTabletFromCache(tableId, partitionKey);

            return tablet;
        }

        /// <summary>
        /// Adds the given tablets to the table location cache.
        /// </summary>
        /// <param name="tableId">The table identifier.</param>
        /// <param name="tablets">The tablets to cache.</param>
        /// <param name="partitionKey">The partition key used to locate the given tablets.</param>
        private void CacheTablets(string tableId, List<RemoteTablet> tablets, ReadOnlySpan<byte> partitionKey)
        {
            TableLocationsCache cache;

            lock (_tableLocations)
            {
                if (!_tableLocations.TryGetValue(tableId, out cache))
                {
                    cache = new TableLocationsCache();
                    _tableLocations.Add(tableId, cache);
                }
            }

            cache.CacheTabletLocations(tablets, partitionKey);
        }

        public ValueTask DisposeAsync()
        {
            return _connectionCache.DisposeAsync();
        }

        public static KuduClient Build(string masterAddresses)
        {
            var masters = masterAddresses.Split(',');

            var options = new KuduClientOptions
            {
                MasterAddresses = new List<HostAndPort>(masters.Length)
            };

            foreach (var master in masters)
            {
                var hostPort = EndpointParser.TryParse(master.Trim(), 7051);
                options.MasterAddresses.Add(hostPort);
            }

            return new KuduClient(options);
        }
    }
}
