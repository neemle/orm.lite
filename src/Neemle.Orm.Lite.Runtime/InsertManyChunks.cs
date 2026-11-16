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
            DbDataSource ds, DbConnection? connection, DbTransaction? transaction, ISqlDialect dialect,
            IReadOnlyList<(string Sql, List<object?> Parameters)> statements)
        {
            if (statements.Count == 1 || transaction is not null)
                return await RunAllAsync(ds, connection, transaction, dialect, statements);
            return await RunInOwnTransactionAsync(ds, connection, dialect, statements);
        }

        private static async Task<int> RunInOwnTransactionAsync(
            DbDataSource ds, DbConnection? connection, ISqlDialect dialect,
            IReadOnlyList<(string Sql, List<object?> Parameters)> statements)
        {
            if (connection is not null) return await RunInTransactionOnAsync(ds, connection, dialect, statements);
            await using var opened = await ds.OpenConnectionAsync();
            return await RunInTransactionOnAsync(ds, opened, dialect, statements);
        }

        private static async Task<int> RunInTransactionOnAsync(
            DbDataSource ds, DbConnection connection, ISqlDialect dialect,
            IReadOnlyList<(string Sql, List<object?> Parameters)> statements)
        {
            await using var tx = await connection.BeginTransactionAsync();
            var affected = await RunAllAsync(ds, connection, tx, dialect, statements);
            await tx.CommitAsync();
            return affected;
        }

        private static async Task<int> RunAllAsync(
            DbDataSource ds, DbConnection? connection, DbTransaction? transaction, ISqlDialect dialect,
            IReadOnlyList<(string Sql, List<object?> Parameters)> statements)
        {
            var affected = 0;
            foreach (var (sql, ps) in statements)
            {
                await using var cmd = StatementCommand.Create(ds, connection, transaction, sql);
                for (var i = 0; i < ps.Count; i++) cmd.AddParam(dialect.Param(i).TrimStart('@'), ps[i] ?? DBNull.Value);
                affected += await cmd.ExecuteNonQueryAsync();
            }
            return affected;
        }
    }
