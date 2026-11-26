using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Neemle.OrmLite.Tests.Graph.Security;

internal sealed class SecurityCommand(DbCommand inner, SecuritySource source) : DbCommand
{
    internal bool WasDisposed { get; private set; }

    [AllowNull]
    public override string CommandText { get => inner.CommandText; set => inner.CommandText = value!; }
    public override int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
    public override CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
    public override bool DesignTimeVisible { get => inner.DesignTimeVisible; set => inner.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource
    {
        get => inner.UpdatedRowSource;
        set => inner.UpdatedRowSource = value;
    }
    protected override DbConnection? DbConnection
    {
        get => inner.Connection;
        set => inner.Connection = value is SecurityConnection tracked ? tracked.Inner : value;
    }
    protected override DbTransaction? DbTransaction { get => inner.Transaction; set => inner.Transaction = value; }
    protected override DbParameterCollection DbParameterCollection => inner.Parameters;
    protected override DbParameter CreateDbParameter() => inner.CreateParameter();
    public override void Cancel() => inner.Cancel();
    public override void Prepare() => inner.Prepare();
    public override int ExecuteNonQuery() => inner.ExecuteNonQuery();
    public override object? ExecuteScalar() => inner.ExecuteScalar();
    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
        inner.ExecuteNonQueryAsync(cancellationToken);
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
        inner.ExecuteScalarAsync(cancellationToken);
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => inner.ExecuteReader(behavior);

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
        CancellationToken cancellationToken)
    {
        source.Capture(inner);
        await Task.Yield();
        if (source.FailRelated && CommandText.Contains(SecurityModel.Quote(SecurityModel.LeafTable),
                StringComparison.Ordinal)) throw source.Timeout;
        var reader = await inner.ExecuteReaderAsync(behavior, cancellationToken);
        source.Readers.Add(reader);
        return reader;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        WasDisposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        WasDisposed = true;
        GC.SuppressFinalize(this);
    }
}
