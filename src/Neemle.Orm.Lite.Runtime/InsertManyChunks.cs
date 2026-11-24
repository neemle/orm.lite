#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

    internal static class InsertManyChunks
    {
        public static IReadOnlyList<IReadOnlyList<ResolvedRow>> Split(IReadOnlyList<ResolvedRow> rows, int capacity)
        {
            if (rows.Count == 0 || rows[0].Values.Count == 0) return new[] { rows };
            var width = rows[0].Values.Count;
            var perChunk = capacity / width;
            if (perChunk <= 0)
                throw new InvalidOperationException(
                    $"a row of {width} bound columns exceeds the dialect's capacity of {capacity} bind parameters per statement.");
            if (rows.Count <= perChunk) return new[] { rows };
            var all = new ResolvedRow[rows.Count];
            for (var i = 0; i < all.Length; i++) all[i] = rows[i];
            var chunks = new List<IReadOnlyList<ResolvedRow>>((all.Length + perChunk - 1) / perChunk);
            for (var start = 0; start < all.Length; start += perChunk)
                chunks.Add(new ArraySegment<ResolvedRow>(all, start, Math.Min(perChunk, all.Length - start)));
            return chunks;
        }

        public static async Task<int> RunAsync(
            ISqlProvider provider, DbDataSource ds, DbConnection? connection, DbTransaction? transaction,
            IReadOnlyList<Statement> statements)
        {
            if (transaction is not null) return await RunAllAsync(ds, connection, transaction, statements);
            if (statements.Count == 1)
            {
                await using var writer = await provider.EnterWriteAsync(ds);
                return await RunAllAsync(ds, connection, transaction, statements);
            }
            return await RunInOwnTransactionAsync(provider, ds, connection, statements);
        }

        private static async Task<int> RunInOwnTransactionAsync(
            ISqlProvider provider, DbDataSource ds, DbConnection? connection, IReadOnlyList<Statement> statements)
        {
            if (connection is not null) return await RunInTransactionOnAsync(provider, ds, connection, statements);
            await using var opened = await ds.OpenConnectionAsync();
            return await RunInTransactionOnAsync(provider, ds, opened, statements);
        }

        private static async Task<int> RunInTransactionOnAsync(
            ISqlProvider provider, DbDataSource ds, DbConnection connection, IReadOnlyList<Statement> statements)
        {
            var started = await provider.BeginTransactionAsync(connection, ds);
            await using var release = started.Release;
            await using var tx = started.Transaction;
            using var trace = TransactionTrace.Begin(connection);
            var affected = await RunAllAsync(ds, connection, tx, statements);
            await tx.CommitAsync();
            trace?.Commit();
            return affected;
        }

        private static async Task<int> RunAllAsync(
            DbDataSource ds, DbConnection? connection, DbTransaction? transaction,
            IReadOnlyList<Statement> statements)
        {
            var affected = 0;
            foreach (var statement in statements)
            {
                await using var cmd = StatementCommand.Create(ds, connection, transaction, statement);
                foreach (var parameter in statement.Parameters)
                    cmd.AddParam(parameter.Name, parameter.Value ?? DBNull.Value);
                affected += await cmd.RunNonQueryAsync(statement.Kind);
            }
            return affected;
        }
    }
