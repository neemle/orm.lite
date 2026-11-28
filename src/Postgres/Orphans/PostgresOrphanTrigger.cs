namespace Neemle.OrmLite.Postgres;

internal sealed record PostgresOrphanTrigger(string Name, int Events, byte[] Arguments,
    string Definition, long Function);
