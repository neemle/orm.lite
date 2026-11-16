#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

    internal sealed class StatementExecutor
    {
        private readonly DbDataSource _ds;
        private readonly RenderState _state;
        private DbConnection? _conn;
        private DbTransaction? _tx;

        internal StatementExecutor(DbDataSource dataSource, RenderState state)
        {
            _ds = dataSource;
            _state = state;
        }

        internal void On(DbConnection connection, DbTransaction? transaction)
        {
            _conn = connection ?? throw new ArgumentNullException(nameof(connection));
            _tx = transaction;
        }

        internal async Task<List<TOut>> RowsAsync<TOut>(string sql, Func<DbDataReader, TOut> map)
        {
            await using var cmd = Cmd(sql);
            Bind(cmd);
            var list = new List<TOut>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(map(r));
            return list;
        }

        internal async Task<object?> ScalarAsync(string sql)
        {
            await using var cmd = Cmd(sql);
            Bind(cmd);
            return await cmd.ExecuteScalarAsync();
        }

        internal async Task<int> NonQueryAsync(string sql)
        {
            await using var cmd = Cmd(sql);
            Bind(cmd);
            return await cmd.ExecuteNonQueryAsync();
        }

        internal async Task<List<object?[]>> ReturningAsync(string sql, int width)
        {
            await using var cmd = Cmd(sql);
            Bind(cmd);
            return await ReadRowsAsync(cmd, width);
        }

        internal async Task<object?[]> AggregateRowAsync(string sql, int width)
        {
            await using var cmd = Cmd(sql);
            Bind(cmd);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return System.Array.Empty<object?>();
            return RowOf(r, width);
        }

        internal async Task<List<object?[]>> UpsertAsync(string text, List<object?> ps, int width, bool refuses)
        {
            await using var cmd = Cmd(text);
            for (var i = 0; i < ps.Count; i++)
                cmd.AddParam(_state.Dialect.Param(i).TrimStart('@'), ps[i] ?? DBNull.Value);
            if (width == 0)
            {
                var affected = await cmd.ExecuteNonQueryAsync();
                if (refuses && affected == 0) throw new SoftDeletedConflictException(_state.Table);
                return new List<object?[]>();
            }
            var rows = await ReadRowsAsync(cmd, width);
            if (refuses && rows.Count == 0) throw new SoftDeletedConflictException(_state.Table);
            return rows;
        }

        internal Task<int> InsertManyAsync(IReadOnlyList<(string Sql, List<object?> Parameters)> statements) =>
            InsertManyChunks.RunAsync(_ds, _conn, _tx, _state.Dialect, statements);

        private DbCommand Cmd(string sql) => StatementCommand.Create(_ds, _conn, _tx, sql);

        internal void Bind(DbCommand cmd)
        {
            for (var i = 0; i < _state.Params.Count; i++)
                cmd.AddParam(_state.Slot(i).TrimStart('@'), _state.Params[i] ?? (object)DBNull.Value);
        }

        private static async Task<List<object?[]>> ReadRowsAsync(DbCommand cmd, int width)
        {
            var rows = new List<object?[]>();
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) rows.Add(RowOf(rd, width));
            return rows;
        }

        private static object?[] RowOf(DbDataReader rd, int width)
        {
            var row = new object?[width];
            for (var i = 0; i < width; i++) row[i] = rd.IsDBNull(i) ? null : rd.GetValue(i);
            return row;
        }
    }
