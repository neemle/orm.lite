namespace Neemle.OrmLite;

internal sealed class SchemaStatementBudget(int limit)
{
    internal bool Cleanup { get; set; }
    internal int Statements { get; private set; }
    internal int CleanupStatements { get; private set; }

    internal void Issue()
    {
        if (Cleanup) { CleanupStatements++; return; }
        if (Statements >= limit) throw new SchemaDiffException(SchemaDiffError.LimitExceeded);
        Statements++;
    }
}
