namespace Neemle.OrmLite;

/// <summary>
/// One problem an integrity check reported. <see cref="Message"/> is never null: for Full and Quick it is the
/// database's own text, verbatim; for ForeignKeys the library renders it from the structured fields.
/// </summary>
/// <param name="Kind">The check that reported it.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Table">The table holding the violating row (ForeignKeys only).</param>
/// <param name="RowId">The violating row's rowid, or null for a WITHOUT ROWID table (ForeignKeys only).</param>
/// <param name="ReferencedTable">The table the foreign key references (ForeignKeys only).</param>
/// <param name="ForeignKeyId">The foreign key's id within its table (ForeignKeys only).</param>
public sealed record IntegrityFinding(
    IntegrityCheckMode Kind,
    string Message,
    string? Table = null,
    long? RowId = null,
    string? ReferencedTable = null,
    int? ForeignKeyId = null);
