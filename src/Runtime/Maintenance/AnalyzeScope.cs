namespace Neemle.OrmLite;

/// <summary>What ANALYZE refreshes statistics for. There is no default and no zero member.</summary>
public enum AnalyzeScope
{
    /// <summary>Every table of the database (SQLite: the main schema).</summary>
    Database = 1,

    /// <summary>One table, named by <see cref="AnalyzeRequest.Table"/>.</summary>
    Table,
}
