namespace Neemle.OrmLite;

public sealed record SchemaDiffScope(string DatabaseScopeId, string Namespace, IReadOnlyList<string> OwnedTables);
