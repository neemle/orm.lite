using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class StreamCursor<T>(StreamReadSnapshot<T> snapshot)
{
    private DbConnection? _connection;
    private DbCommand? _command;
    private DbDataReader? _reader;
    private BorrowedStreamLease? _lease;

    internal DbDataReader Reader => _reader ?? throw new QueryStreamException(QueryStreamError.InvalidState);

    internal void Validate()
    {
        if (snapshot.Borrowed is { } borrowed) StreamResourceChecks.Validate(snapshot.Binding, borrowed);
    }

    internal async Task OpenAsync(CancellationToken ct)
    {
        if (snapshot.Borrowed is { } borrowed)
        {
            Validate();
            _lease = BorrowedStreamLease.TryAcquire(borrowed.Connection);
            _connection = borrowed.Connection;
        }
        else
        {
            var source = snapshot.Binding.DataSource;
            _connection = source is GatedDataSource gated
                ? await gated.OpenStreamConnectionAsync(ct) : await source.OpenConnectionAsync(ct);
        }
        ct.ThrowIfCancellationRequested();
        Validate();
        _command = _connection.CreateCommand();
        var statement = snapshot.FreshStatement();
        _command.CommandText = statement.Text;
        _command.Transaction = snapshot.Borrowed?.Transaction;
        foreach (var parameter in statement.Parameters)
            _command.AddParam(parameter.Name, parameter.Value ?? DBNull.Value);
        _reader = await _command.RunReaderAsync(statement.Kind, ct);
        Validate();
    }

    internal async Task CleanupAsync(StreamCleanup cleanup)
    {
        var reader = _reader;
        var command = _command;
        var connection = _connection;
        _reader = null; _command = null; _connection = null;
        var certain = true;
        if (reader is not null) certain &= await cleanup.Attempt(reader.DisposeAsync);
        if (command is not null) certain &= await cleanup.Attempt(command.DisposeAsync);
        if (connection is not null && snapshot.Borrowed is null)
        {
            var disposed = await cleanup.Attempt(connection.DisposeAsync);
            if (disposed) GatedDataSource.LeaseOf(connection)?.ReleaseStream();
        }
        if (_lease is null) return;
        if (!certain) _lease.Uncertain();
        await cleanup.Attempt(_lease.DisposeAsync);
        _lease = null;
    }
}
