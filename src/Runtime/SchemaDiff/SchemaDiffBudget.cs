namespace Neemle.OrmLite;

internal sealed class SchemaDiffBudget(SchemaDiffLimits limits)
{
    private int _columns;
    private int _objects;
    private int _statements;

    internal static void Validate(SchemaDiffLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaxTables < 1 || limits.MaxColumns < 1 || limits.MaxCatalogObjects < 1
            || limits.MaxOperations < 1 || limits.MaxStatements < 1)
            throw new SchemaDiffException(SchemaDiffError.InvalidRequest);
    }

    internal void Column() => Increment(ref _columns, limits.MaxColumns);
    internal void Object() => Increment(ref _objects, limits.MaxCatalogObjects);
    internal void Statement() => Increment(ref _statements, limits.MaxStatements);

    private static void Increment(ref int value, int max)
    {
        if (value >= max) throw new SchemaDiffException(SchemaDiffError.LimitExceeded);
        value++;
    }
}
