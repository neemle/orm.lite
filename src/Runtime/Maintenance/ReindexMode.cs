namespace Neemle.OrmLite;

/// <summary>How REINDEX treats concurrent work. There is no default and no zero member; Concurrent is never downgraded.</summary>
public enum ReindexMode
{
    /// <summary>Rebuilds while blocking writes to the table (and, on PostgreSQL, reads that use the index being rebuilt).</summary>
    Blocking = 1,

    /// <summary>PostgreSQL REINDEX CONCURRENTLY: reads and writes continue; it waits for older transactions.</summary>
    Concurrent,
}
