namespace Neemle.OrmLite;

/// <summary>The VACUUM mode. There is no default and no zero member; <see cref="Plain"/> never becomes FULL.</summary>
public enum VacuumMode
{
    /// <summary>A plain VACUUM: on PostgreSQL it reclaims space for reuse without an exclusive lock; on SQLite it rebuilds the file.</summary>
    Plain = 1,

    /// <summary>PostgreSQL VACUUM (FULL): rewrites the table, returns space to the operating system, holds ACCESS EXCLUSIVE.</summary>
    Full,
}
