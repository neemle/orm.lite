namespace Neemle.OrmLite;

public sealed record SchemaDiffLimits(
    int MaxTables, int MaxColumns, int MaxCatalogObjects, int MaxOperations, int MaxStatements);
