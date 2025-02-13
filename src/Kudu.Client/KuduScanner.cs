﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Builder;
using Kudu.Client.Exceptions;
using Kudu.Client.Protocol;
using Kudu.Client.Protocol.Rpc;
using Kudu.Client.Protocol.Tserver;
using Kudu.Client.Requests;
using Kudu.Client.Scanner;
using Kudu.Client.Tablet;
using Kudu.Client.Util;
using ProtoBuf;

namespace Kudu.Client
{
    public class KuduScanner : IAsyncEnumerable<ResultSet>
    {
        private readonly ScanBuilder _scanBuilder;

        public KuduScanner(ScanBuilder scanBuilder)
        {
            _scanBuilder = scanBuilder;
        }

        public KuduScanEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var partitionPruner = PartitionPruner.Create(
                _scanBuilder.Table.Schema,
                _scanBuilder.Table.PartitionSchema,
                _scanBuilder.Predicates,
                _scanBuilder.LowerBoundPrimaryKey,
                _scanBuilder.UpperBoundPrimaryKey,
                _scanBuilder.LowerBoundPartitionKey,
                _scanBuilder.UpperBoundPartitionKey);

            return new KuduScanEnumerator(
                _scanBuilder.Client,
                _scanBuilder.Table,
                _scanBuilder.ProjectedColumns,
                _scanBuilder.ReadMode,
                _scanBuilder.IsFaultTolerant,
                _scanBuilder.Predicates,
                _scanBuilder.Limit,
                _scanBuilder.CacheBlocks,
                _scanBuilder.LowerBoundPrimaryKey,
                _scanBuilder.UpperBoundPrimaryKey,
                _scanBuilder.StartTimestamp,
                _scanBuilder.HtTimestamp,
                _scanBuilder.BatchSizeBytes,
                partitionPruner,
                _scanBuilder.ReplicaSelection,
                cancellationToken);
        }

        IAsyncEnumerator<ResultSet> IAsyncEnumerable<ResultSet>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }

    public class KuduScanEnumerator : IAsyncEnumerator<ResultSet>
    {
        private readonly KuduClient _client;
        private readonly KuduTable _table;
        private readonly List<ColumnSchemaPB> _columns;
        private readonly Schema _schema;
        private readonly PartitionPruner _partitionPruner;
        private readonly Dictionary<string, KuduPredicate> _predicates;
        private readonly CancellationToken _cancellationToken;

        private readonly OrderModePB _orderMode;
        private readonly ReadMode _readMode;
        private readonly ReplicaSelection _replicaSelection;
        private readonly bool _isFaultTolerant;
        private readonly int _batchSizeBytes;
        private readonly long _limit;
        private readonly bool _cacheBlocks;
        private readonly long _startTimestamp;
        private readonly long _lowerBoundPropagationTimestamp = KuduClient.NoTimestamp;

        private readonly byte[] _startPrimaryKey;
        private readonly byte[] _endPrimaryKey;

        private bool _closed;
        private long _numRowsReturned;
        private long _htTimestamp;
        private uint _sequenceId;
        private byte[] _scannerId;
        private byte[] _lastPrimaryKey;

        /// <summary>
        /// The tabletSlice currently being scanned.
        /// If null, we haven't started scanning.
        /// If == DONE, then we're done scanning.
        /// Otherwise it contains a proper tabletSlice name, and we're currently scanning.
        /// </summary>
        private RemoteTablet _tablet;

        public ResultSet Current { get; private set; }

        public KuduScanEnumerator(
            KuduClient client, KuduTable table,
            List<string> projectedNames,
            ReadMode readMode,
            bool isFaultTolerant,
            Dictionary<string, KuduPredicate> predicates,
            long limit,
            bool cacheBlocks,
            byte[] startPrimaryKey, byte[] endPrimaryKey,
            long startTimestamp, long htTimestamp,
            int? batchSizeBytes,
            PartitionPruner partitionPruner,
            ReplicaSelection replicaSelection,
            CancellationToken cancellationToken)
        {
            //    checkArgument(batchSizeBytes >= 0, "Need non-negative number of bytes, " +
            //"got %s", batchSizeBytes);
            //    checkArgument(limit > 0, "Need a strictly positive number for the limit, " +
            //        "got %s", limit);

            if (htTimestamp != KuduClient.NoTimestamp)
            {
                //checkArgument(htTimestamp >= 0, "Need non-negative number for the scan, " +
                //    " timestamp got %s", htTimestamp);
                //checkArgument(readMode == ReadMode.READ_AT_SNAPSHOT, "When specifying a " +
                //    "HybridClock timestamp, the read mode needs to be set to READ_AT_SNAPSHOT");
            }
            if (startTimestamp != KuduClient.NoTimestamp)
            {
                //checkArgument(htTimestamp >= 0, "Must have both start and end timestamps " +
                //              "for a diff scan");
                //checkArgument(startTimestamp <= htTimestamp, "Start timestamp must be less " +
                //              "than or equal to end timestamp");
            }

            _isFaultTolerant = isFaultTolerant;
            if (isFaultTolerant)
            {
                if (readMode != ReadMode.ReadAtSnapshot)
                    throw new ArgumentException("Use of fault tolerance scanner " +
                        "requires the read mode to be set to READ_AT_SNAPSHOT");
                _orderMode = OrderModePB.Ordered;
            }
            else
                _orderMode = OrderModePB.Unordered;

            _client = client;
            _table = table;
            _partitionPruner = partitionPruner;
            _readMode = readMode;
            _predicates = predicates;
            _limit = limit;
            _cacheBlocks = cacheBlocks;
            _startPrimaryKey = startPrimaryKey ?? Array.Empty<byte>();
            _endPrimaryKey = endPrimaryKey ?? Array.Empty<byte>();
            _startTimestamp = startTimestamp;
            _htTimestamp = htTimestamp;
            _lastPrimaryKey = Array.Empty<byte>();
            _cancellationToken = cancellationToken;

            // Map the column names to actual columns in the table schema.
            // If the user set this to 'null', we scan all columns.
            _columns = new List<ColumnSchemaPB>();
            var columns = new List<ColumnSchema>();
            if (projectedNames != null)
            {
                foreach (string columnName in projectedNames)
                {
                    ColumnSchema originalColumn = table.Schema.GetColumn(columnName);
                    _columns.Add(ToColumnSchemaPb(originalColumn));
                    columns.Add(originalColumn);
                }
            }
            else
            {
                foreach (ColumnSchema columnSchema in table.Schema.Columns)
                {
                    _columns.Add(ToColumnSchemaPb(columnSchema));
                    columns.Add(columnSchema);
                }
            }
            _schema = new Schema(columns);
            _batchSizeBytes = batchSizeBytes ?? GetScannerBatchSizeEstimate(_schema);
            // This is a diff scan so add the IS_DELETED column.
            if (startTimestamp != KuduClient.NoTimestamp)
            {
                _columns.Add(GenerateIsDeletedColumn(table.Schema));
            }

            // If the partition pruner has pruned all partitions, then the scan can be
            // short circuited without contacting any tablet servers.
            if (!_partitionPruner.HasMorePartitionKeyRanges)
            {
                _closed = true;
            }

            _replicaSelection = replicaSelection;

            // For READ_YOUR_WRITES scan mode, get the latest observed timestamp
            // and store it. Always use this one as the propagated timestamp for
            // the duration of the scan to avoid unnecessary wait.
            if (readMode == ReadMode.ReadYourWrites)
            {
                _lowerBoundPropagationTimestamp = client.LastPropagatedTimestamp;
            }
        }

        /// <summary>
        /// Generates and returns a ColumnSchema for the virtual IS_DELETED column.
        /// The column name is generated to ensure there is never a collision.
        /// </summary>
        /// <param name="schema">The table schema.</param>
        private static ColumnSchemaPB GenerateIsDeletedColumn(Schema schema)
        {
            var columnName = "is_deleted";

            // If the column already exists and we need to pick an alternate column name.
            while (schema.HasColumn(columnName))
            {
                columnName += "_";
            }

            return new ColumnSchemaPB
            {
                Name = columnName,
                Type = DataTypePB.IsDeleted,
                ReadDefaultValue = new byte[] { 0 },
                IsNullable = false,
                IsKey = false
            };
        }

        public async ValueTask DisposeAsync()
        {
            if (Current != null)
            {
                Current.Dispose();
                Current = null;
            }

            if (!_closed)
            {
                // Getting a null tablet here without being in a closed state
                // means we were in between tablets.
                if (_tablet != null)
                {
                    ScanRequest rpc = GetCloseRequest();
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5))) {

                        try {
                            await _client.SendRpcToTabletAsync(rpc, cts.Token)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex) {
                            // TODO: Log warning.
                            Console.WriteLine($"Error closing scanner: {ex}");
                        }
                    }
                }

                _closed = true;
                Invalidate();
            }
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            // TODO: Catch OperationCancelledException, and call DisposeAsync()
            // Only wait a small amount of time to cancel the scan on the server.

            if (Current != null)
            {
                Current.Dispose();
                Current = null;
            }

            if (_closed)
            {
                // We're already done scanning.
                return false;
            }
            else if (_tablet == null)
            {
                // We need to open the scanner first.
                var rpc = GetOpenRequest();
                await _client.SendRpcToTabletAsync(rpc, _cancellationToken)
                    .ConfigureAwait(false);

                _tablet = rpc.Tablet;

                var resp = rpc.Response;

                if (_htTimestamp == KuduClient.NoTimestamp &&
                    resp.ScanTimestamp != KuduClient.NoTimestamp)
                {
                    // If the server-assigned timestamp is present in the tablet
                    // server's response, store it in the scanner. The stored value
                    // is used for read operations in READ_AT_SNAPSHOT mode at
                    // other tablet servers in the context of the same scan.
                    _htTimestamp = resp.ScanTimestamp;
                }

                long lastPropagatedTimestamp = KuduClient.NoTimestamp;
                if (_readMode == ReadMode.ReadYourWrites &&
                    resp.ScanTimestamp != KuduClient.NoTimestamp)
                {
                    // For READ_YOUR_WRITES mode, update the latest propagated timestamp
                    // with the chosen snapshot timestamp sent back from the server, to
                    // avoid unnecessarily wait for subsequent reads. Since as long as
                    // the chosen snapshot timestamp of the next read is greater than
                    // the previous one, the scan does not violate READ_YOUR_WRITES
                    // session guarantees.
                    lastPropagatedTimestamp = resp.ScanTimestamp;
                }
                else if (resp.PropagatedTimestamp != KuduClient.NoTimestamp)
                {
                    // Otherwise we just use the propagated timestamp returned from
                    // the server as the latest propagated timestamp.
                    lastPropagatedTimestamp = resp.PropagatedTimestamp;
                }
                if (lastPropagatedTimestamp != KuduClient.NoTimestamp)
                {
                    _client.LastPropagatedTimestamp = lastPropagatedTimestamp;
                }

                if (_isFaultTolerant && resp.LastPrimaryKey != null)
                {
                    _lastPrimaryKey = resp.LastPrimaryKey;
                }

                _numRowsReturned += resp.NumRows;
                Current = resp.Data;

                if (!resp.HasMoreResults || resp.ScannerId == null)
                {
                    ScanFinished();
                    return resp.NumRows > 0;
                }

                _scannerId = resp.ScannerId;
                _sequenceId++;

                return resp.NumRows > 0;
            }
            else
            {
                var rpc = GetNextRowsRequest();
                await _client.SendRpcToTabletAsync(rpc, _cancellationToken)
                    .ConfigureAwait(false);

                _tablet = rpc.Tablet;

                var resp = rpc.Response;

                _numRowsReturned += resp.NumRows;
                Current = resp.Data;

                if (!resp.HasMoreResults)
                {  // We're done scanning this tablet.
                    ScanFinished();
                    return resp.NumRows > 0;
                }
                _sequenceId++;
                return resp.NumRows > 0;
            }
        }

        private ColumnSchemaPB ToColumnSchemaPb(ColumnSchema columnSchema)
        {
            // TODO: Move this to shared location.
            return new ColumnSchemaPB
            {
                Name = columnSchema.Name,
                Type = (DataTypePB)columnSchema.Type,
                IsNullable = columnSchema.IsNullable,
                // Se isKey to false on the passed ColumnSchema.
                // This allows out of order key columns in projections.
                IsKey = false,
                // TODO: Default values
                // TODO: Block size
                Encoding = (EncodingTypePB)columnSchema.Encoding,
                Compression = (CompressionTypePB)columnSchema.Compression,
                TypeAttributes = columnSchema.TypeAttributes == null ? null : new ColumnTypeAttributesPB
                {
                    Precision = columnSchema.TypeAttributes.Precision,
                    Scale = columnSchema.TypeAttributes.Scale
                }
                // TODO: Comment
            };
        }

        private ScanRequest GetOpenRequest()
        {
            //checkScanningNotStarted();
            return new ScanRequest(this, State.Opening);
        }

        private ScanRequest GetNextRowsRequest()
        {
            //checkScanningNotStarted();
            return new ScanRequest(this, State.Next);
        }

        private ScanRequest GetCloseRequest()
        {
            return new ScanRequest(this, State.Closing);
        }

        private void ScanFinished()
        {
            Partition partition = _tablet.Partition;
            _partitionPruner.RemovePartitionKeyRange(partition.PartitionKeyEnd);
            // Stop scanning if we have scanned until or past the end partition key, or
            // if we have fulfilled the limit.
            if (!_partitionPruner.HasMorePartitionKeyRanges || _numRowsReturned >= _limit)
            {
                _closed = true; // The scanner is closed on the other side at this point.
                return;
            }

            //Console.WriteLine($"Done scanning tablet {_tablet.TabletId} for partition {_tablet.Partition} with scanner id {BitConverter.ToString(_scannerId)}");

            _scannerId = null;
            _sequenceId = 0;
            _lastPrimaryKey = Array.Empty<byte>();
            Invalidate();
        }

        /// <summary>
        /// Invalidates this scanner and makes it assume it's no longer opened.
        /// When a TabletServer goes away while we're scanning it, or some other type
        /// of access problem happens, this method should be called so that the
        /// scanner will have to re-locate the TabletServer and re-open itself.
        /// </summary>
        private void Invalidate()
        {
            _tablet = null;
        }

        private class ScanRequest : KuduTabletRpc
        {
            private readonly KuduScanEnumerator _scanner;
            private readonly State _state;

            private ScanResponsePB _responsePB;

            public ScanRequest(KuduScanEnumerator scanner, State state)
            {
                _scanner = scanner;
                _state = state;
                Tablet = scanner._tablet;
                TableId = _scanner._table.TableId;
                PartitionKey = _scanner._partitionPruner.NextPartitionKey;
            }

            public override string MethodName => "Scan";

            // TODO: Required features

            public override ReplicaSelection ReplicaSelection => _scanner._replicaSelection;

            // TODO: This should be generic.
            public ScanResponse<ResultSet> Response { get; private set; }

            // TODO: Authz token

            public override void WriteRequest(Stream stream)
            {
                var request = new ScanRequestPB();

                if (_state == State.Opening)
                {
                    var newRequest = request.NewScanRequest = new NewScanRequestPB();
                    newRequest.Limit = (ulong)(_scanner._limit - _scanner._numRowsReturned);
                    newRequest.ProjectedColumns.AddRange(_scanner._columns);
                    newRequest.TabletId = Tablet.TabletId.ToUtf8ByteArray();
                    newRequest.OrderMode = _scanner._orderMode;
                    newRequest.CacheBlocks = _scanner._cacheBlocks;
                    // If the last propagated timestamp is set, send it with the scan.
                    // For READ_YOUR_WRITES scan, use the propagated timestamp from
                    // the scanner.
                    long timestamp;
                    if (_scanner._readMode == ReadMode.ReadYourWrites)
                    {
                        timestamp = _scanner._lowerBoundPropagationTimestamp;
                    }
                    else
                    {
                        timestamp = _scanner._client.LastPropagatedTimestamp;
                    }
                    if (timestamp != KuduClient.NoTimestamp)
                    {
                        newRequest.PropagatedTimestamp = (ulong)timestamp;
                    }
                    newRequest.ReadMode = (ReadModePB)_scanner._readMode;

                    // If the mode is set to read on snapshot set the snapshot timestamps.
                    if (_scanner._readMode == ReadMode.ReadAtSnapshot)
                    {
                        if (_scanner._htTimestamp != KuduClient.NoTimestamp)
                            newRequest.SnapTimestamp = (ulong)_scanner._htTimestamp;

                        if (_scanner._startTimestamp != KuduClient.NoTimestamp)
                            newRequest.SnapStartTimestamp = (ulong)_scanner._startTimestamp;
                    }

                    if (_scanner._isFaultTolerant)
                    {
                        if (_scanner._lastPrimaryKey.Length > 0)
                        {
                            newRequest.LastPrimaryKey = _scanner._lastPrimaryKey;
                        }
                    }

                    if (_scanner._startPrimaryKey.Length > 0)
                    {
                        newRequest.StartPrimaryKey = _scanner._startPrimaryKey;
                    }

                    if (_scanner._endPrimaryKey.Length > 0)
                    {
                        newRequest.StopPrimaryKey = _scanner._endPrimaryKey;
                    }

                    foreach (KuduPredicate predicate in _scanner._predicates.Values)
                    {
                        newRequest.ColumnPredicates.Add(predicate.ToProtobuf());
                    }
                    //if (authzToken != null)
                    //{
                    //    newBuilder.setAuthzToken(authzToken);
                    //}
                    request.BatchSizeBytes = (uint)_scanner._batchSizeBytes;
                }
                else if (_state == State.Next)
                {
                    request.ScannerId = _scanner._scannerId;
                    request.CallSeqId = _scanner._sequenceId;
                    request.BatchSizeBytes = (uint)_scanner._batchSizeBytes;
                }
                else if (_state == State.Closing)
                {
                    request.ScannerId = _scanner._scannerId;
                    request.BatchSizeBytes = 0;
                    request.CloseScanner = true;
                }

                Serializer.SerializeWithLengthPrefix(stream, request, PrefixStyle.Base128);
            }

            public override void ParseProtobuf(ReadOnlySequence<byte> buffer)
            {
                var resp = Serializer.Deserialize<ScanResponsePB>(buffer.ToStream());

                // TODO: Error handling

                _responsePB = resp;
            }

            public override async Task ParseSidecarsAsync(
                ResponseHeader header, PipeReader reader, int length)
            {
                // Allocate buffers for sidecars.
                var sidecars = AllocateSidecars(header, length);
                var nextSidecarIndex = 0;
                var currentSidecar = sidecars[nextSidecarIndex++].Memory;

                long remaining = length;
                long remainingSidecarLength = currentSidecar.Length;

                try
                {
                    // Fill sidecars.
                    while (remaining > 0)
                    {
                        // Don't pass the cancellation token. Even if the scan
                        // is cancelled, we still need to consume all the data.
                        var result = await reader.ReadAsync().ConfigureAwait(false);

                        if (result.IsCanceled || result.IsCompleted)
                            break;

                        var buffer = result.Buffer;

                        do
                        {
                            if (currentSidecar.Length == 0)
                            {
                                currentSidecar = sidecars[nextSidecarIndex++].Memory;
                                remainingSidecarLength = currentSidecar.Length;
                            }

                            // How much data can we copy from the buffer?
                            var bytesToRead = Math.Min(buffer.Length, remainingSidecarLength);
                            buffer.Slice(0, bytesToRead).CopyTo(currentSidecar.Span);

                            remaining -= bytesToRead;
                            remainingSidecarLength -= bytesToRead;
                            currentSidecar = currentSidecar.Slice((int)bytesToRead);

                            buffer = buffer.Slice(bytesToRead);
                        }
                        while (remaining > 0 && buffer.Length > 0);

                        reader.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
                finally
                {
                    if (remaining > 0)
                    {
                        foreach (var sidecar in sidecars)
                            sidecar.Dispose();

                        throw new NonRecoverableException(KuduStatus.IllegalState(
                            $"Unable to read all sidecar data ({remaining}/{length})"));
                    }
                }

                var response = _responsePB;

                var rowData = sidecars[response.Data.RowsSidecar];
                IMemoryOwner<byte> indirectData = null;

                if (response.Data.ShouldSerializeIndirectDataSidecar())
                {
                    indirectData = sidecars[response.Data.IndirectDataSidecar];
                }

                var resultSet = new ResultSet(
                    _scanner._schema,
                    response.Data.NumRows,
                    rowData,
                    indirectData);

                Response = new ScanResponse<ResultSet>(
                    response.ScannerId,
                    resultSet,
                    response.Data.NumRows,
                    response.HasMoreResults,
                    response.ShouldSerializeSnapTimestamp() ? (long)response.SnapTimestamp : KuduClient.NoTimestamp,
                    response.ShouldSerializePropagatedTimestamp() ? (long)response.PropagatedTimestamp : KuduClient.NoTimestamp,
                    response.LastPrimaryKey);
            }

            private static List<ArrayMemoryPoolBuffer<byte>> AllocateSidecars(
                ResponseHeader header, int length)
            {
                var sidecars = new List<ArrayMemoryPoolBuffer<byte>>();
                int total = 0;
                for (int i = 0; i < header.SidecarOffsets.Length; i++)
                {
                    var offset = header.SidecarOffsets[i];
                    int size;
                    if (i + 1 >= header.SidecarOffsets.Length)
                    {
                        size = length - total;
                    }
                    else
                    {
                        var next = header.SidecarOffsets[i + 1];
                        size = (int)(next - offset);
                    }

                    var sidecar = new ArrayMemoryPoolBuffer<byte>(size);
                    sidecars.Add(sidecar);

                    total += size;
                }

                if (total != length)
                {
                    foreach (var sidecar in sidecars)
                        sidecar.Dispose();

                    throw new NonRecoverableException(KuduStatus.IllegalState(
                        $"Expected {length} sidecar bytes, computed {total}"));
                }

                return sidecars;
            }
        }

        private static int GetScannerBatchSizeEstimate(Schema schema)
        {
            if (schema.VarLengthColumnCount == 0)
            {
                // No variable length data, we can do an
                // exact ideal estimate here.
                return 1024 * 1024 - schema.RowSize;
            }
            else
            {
                // Assume everything evens out.
                // Most of the time it probably does.
                return 1024 * 1024;
            }
        }

        private enum State
        {
            Opening,
            Next,
            Closing
        }

        private sealed class ArrayMemoryPoolBuffer<T> : IMemoryOwner<T>
        {
            private readonly int _length;
            private T[] _array;

            public ArrayMemoryPoolBuffer(int size)
            {
                _length = size;
                _array = ArrayPool<T>.Shared.Rent(size);
            }

            public Memory<T> Memory
            {
                get
                {
                    T[] array = _array;
                    if (array == null)
                    {
                        throw new ObjectDisposedException(nameof(ArrayMemoryPoolBuffer<T>));
                    }

                    return new Memory<T>(array, 0, _length);
                }
            }

            public void Dispose()
            {
                T[] array = _array;
                if (array != null)
                {
                    _array = null;
                    ArrayPool<T>.Shared.Return(array);
                }
            }
        }
    }
}
