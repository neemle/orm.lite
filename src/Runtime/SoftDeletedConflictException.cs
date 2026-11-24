#nullable enable
using System;

namespace Neemle.OrmLite;

public sealed class SoftDeletedConflictException(string table) : InvalidOperationException(
    $"the upsert into \"{table}\" conflicts with a soft-deleted row, and an upsert never revives or overwrites one; "
    + "restore it explicitly, or purge it, and retry.")
{
    public string Table { get; } = table;
}
