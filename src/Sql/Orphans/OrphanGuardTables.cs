namespace Neemle.OrmLite.Sql;

// The parent and child table names, already qualified and quoted by the provider, that the guard's two observation
// reads (OrphanGuardSql.Parent and Children) select from.
internal sealed record OrphanGuardTables(string Parent, string Child);
