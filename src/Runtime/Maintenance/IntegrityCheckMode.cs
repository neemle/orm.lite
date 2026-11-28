namespace Neemle.OrmLite;

/// <summary>Which integrity check runs. There is no default and no zero member.</summary>
public enum IntegrityCheckMode
{
    /// <summary>SQLite <c>integrity_check</c>: the file structure, every index against its table, constraints; not foreign keys.</summary>
    Full = 1,

    /// <summary>SQLite <c>quick_check</c>: the file structure without comparing indexes with their tables; faster.</summary>
    Quick,

    /// <summary>SQLite <c>foreign_key_check</c>: rows whose declared foreign key references a missing parent row.</summary>
    ForeignKeys,
}
