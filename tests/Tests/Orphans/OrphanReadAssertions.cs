using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

internal static class OrphanReadAssertions
{
    internal static void Refused<T>(OrphanReadDatabase database, OrphanReadRow<T> row,
        OrphanError error = OrphanError.UnsupportedVisibility)
    {
        var statements = database.Statements.Count;
        var read = row.ValuesRead;
        var failure = Assert.Throws<OrphanException>(() => ((IOrphanEntity)OrphanReadModel<T>.Binding).Values(row));
        Assert.Equal(error, failure.Error);
        Assert.Equal(OrphanOutcome.NotStarted, failure.Outcome);
        Assert.Equal(read, row.ValuesRead);
        Assert.Equal(statements, database.Statements.Count);
    }

    internal static void Full<T>(OrphanReadRow<T> row)
    {
        var read = row.ValuesRead;
        Assert.Equal([row.Id, row.ParentId, row.Name], ((IOrphanEntity)OrphanReadModel<T>.Binding).Values(row));
        Assert.Equal(read + 1, row.ValuesRead);
    }

    internal static async Task<T> One<T>(IAsyncEnumerable<T> stream)
    {
        var rows = new List<T>();
        await foreach (var row in stream) rows.Add(row);
        return Assert.Single(rows);
    }
}
