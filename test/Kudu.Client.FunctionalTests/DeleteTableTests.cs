﻿using System;
using System.Threading.Tasks;
using Kudu.Client.Builder;
using Kudu.Client.Exceptions;
using Kudu.Client.FunctionalTests.MiniCluster;
using McMaster.Extensions.Xunit;
using Xunit;

namespace Kudu.Client.FunctionalTests
{
    [MiniKuduClusterTest]
    public class DeleteTableTests
    {
        [SkippableFact]
        public async Task CreateAndDeleteTable()
        {
            using var miniCluster = new MiniKuduClusterBuilder()
                .NumMasters(3)
                .NumTservers(3)
                .Create();

            await using var client = miniCluster.CreateClient();

            var tableName = Guid.NewGuid().ToString();
            var builder = new TableBuilder()
                .SetTableName(tableName)
                .SetNumReplicas(1)
                .AddColumn(column =>
                {
                    column.Name = "pk";
                    column.Type = KuduType.Int32;
                    column.IsKey = true;
                    column.IsNullable = false;
                });

            var table = await client.CreateTableAsync(builder);
            Assert.Equal(tableName, table.TableName);

            await client.DeleteTableAsync(tableName);

            await Assert.ThrowsAsync<MasterException>(() => client.DeleteTableAsync(tableName));
        }
    }
}
