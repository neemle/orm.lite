namespace Neemle.OrmLite;

/// <summary>What REINDEX rebuilds. There is no default and no zero member.</summary>
public enum ReindexScope
{
    /// <summary>Every index of the database (PostgreSQL: system catalogs excluded).</summary>
    Database = 1,

    /// <summary>Every index of one table, implicit primary-key and unique indexes included.</summary>
    Table,

    /// <summary>One declared index of one table.</summary>
    Index,
}
