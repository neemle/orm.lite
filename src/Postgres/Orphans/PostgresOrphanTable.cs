namespace Neemle.OrmLite.Postgres;

internal sealed record PostgresOrphanTable(long Oid, string Sql, bool Ordinary);
