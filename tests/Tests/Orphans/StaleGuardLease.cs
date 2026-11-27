using System.Data.Common;

namespace Neemle.OrmLite.Tests.Orphans;

// The real guard lease; once its effect verification has passed, the stale statement runs inside the scope.
internal sealed class StaleGuardLease(IOrphanGuardLease inner, DbConnection connection, DbTransaction transaction,
    string sql, object?[] values) : IOrphanGuardLease
{
    public IReadOnlyList<object> OriginalKeys => inner.OriginalKeys;
    public int StatementsUsed => inner.StatementsUsed;

    public async Task VerifyEffectsAsync(OrphanMode mode, CancellationToken ct)
    {
        await inner.VerifyEffectsAsync(mode, ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.AddParam("p" + i, values[i]);
        if (await command.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("the stale statement must change exactly one row: " + sql);
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
