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

        internal async Task<List<TOut>> RowsAsync<TOut>(Statement statement, Func<DbDataReader, TOut> map)
        {
            await using var cmd = Cmd(statement);
            Bind(cmd, statement);
            var list = new List<TOut>();
            await using var r = await cmd.RunReaderAsync(statement.Kind);
            while (await r.ReadAsync()) list.Add(map(r));
            return list;
        }

        internal async Task<object?> ScalarAsync(Statement statement)
        {
            await using var cmd = Cmd(statement);
            Bind(cmd, statement);
            return await cmd.RunScalarAsync(statement.Kind);
        }

        internal async Task<int> NonQueryAsync(Statement statement)
        {
            await using var writer = await WriterAsync(statement);
            await using var cmd = Cmd(statement);
            Bind(cmd, statement);
            return await cmd.RunNonQueryAsync(statement.Kind);
        }

        internal async Task<List<object?[]>> ReturningAsync(Statement statement, int width)
        {
            await using var writer = await WriterAsync(statement);
            await using var cmd = Cmd(statement);
            Bind(cmd, statement);
            return await ReadRowsAsync(cmd, statement.Kind, width);
        }

        internal async Task<object?[]> AggregateRowAsync(Statement statement, int width)
        {
            await using var cmd = Cmd(statement);
            Bind(cmd, statement);
            await using var r = await cmd.RunReaderAsync(statement.Kind);
            if (!await r.ReadAsync()) return System.Array.Empty<object?>();
            return RowOf(r, width);
        }

        internal async Task<List<object?[]>> UpsertAsync(Statement statement, int width, bool refuses)
        {
            await using var writer = await WriterAsync(statement);
            await using var cmd = Cmd(statement);
            Bind(cmd, statement);
            if (width == 0)
            {
                var affected = await cmd.RunNonQueryAsync(statement.Kind);
                if (refuses && affected == 0) throw new SoftDeletedConflictException(_state.Table);
                return new List<object?[]>();
            }
            var rows = await ReadRowsAsync(cmd, statement.Kind, width);
            if (refuses && rows.Count == 0) throw new SoftDeletedConflictException(_state.Table);
            return rows;
        }

        internal Task<int> InsertManyAsync(IReadOnlyList<Statement> statements) =>
            InsertManyChunks.RunAsync(_state.Provider, _ds, _conn, _tx, statements);

        private async ValueTask<WriterLease> WriterAsync(Statement statement) =>
            new(statement.Kind == StatementKind.Write && _tx is null
                ? await _state.Provider.EnterWriteAsync(_ds)
                : null);

        private DbCommand Cmd(Statement statement) => StatementCommand.Create(_ds, _conn, _tx, statement);

        private static void Bind(DbCommand cmd, Statement statement)
        {
            foreach (var parameter in statement.Parameters)
                cmd.AddParam(parameter.Name, parameter.Value ?? (object)DBNull.Value);
        }

        internal void Bind(DbCommand cmd)
        {
            for (var i = 0; i < _state.Params.Count; i++)
                cmd.AddParam(_state.Slot(i).TrimStart('@'), _state.Params[i] ?? (object)DBNull.Value);
        }

        private static async Task<List<object?[]>> ReadRowsAsync(DbCommand cmd, StatementKind kind, int width)
        {
            var rows = new List<object?[]>();
            await using var rd = await cmd.RunReaderAsync(kind);
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

    internal readonly struct WriterLease(IAsyncDisposable? release) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => release?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
