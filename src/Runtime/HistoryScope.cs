#nullable enable
using System;

namespace Neemle.OrmLite;

// The one rule for the tenant scope of every statement that reads or writes the history of a soft-delete row: the
// history reads, the revert, the restore and the trash all ask it which column a statement must filter on.
//   - A Platform entry has no tenant. The statement carries no tenant predicate; a tenant id given with it is ignored.
//   - Every other entry is scoped by its tenant column, and that includes an entry whose tenant column is also its key
//     (the tenant's own row): the column is compared with the tenant id like any other.
//   - A non-Platform entry that names no tenant column is refused, and so is a call that gives no tenant id for such an
//     entry: a missing scope never widens a statement to every tenant.
// The trash list is the one place that skips an entry with no tenant column instead of refusing it, because it lists
// many entries at once; it still lists nothing for it.
internal static class HistoryScope
{
    internal static string? Column(string verb, bool platform, string? tenantColumn, bool tenantGiven = true)
    {
        if (platform) return null;
        if (tenantColumn is null || !tenantGiven)
            throw new InvalidOperationException(
                $"{verb} on a tenant entity requires its tenant column and a tenant id.");
        return tenantColumn;
    }

    internal static string? Column(string verb, ISoftEntry entry) => Column(verb, entry.Platform, entry.TenantColumn);
}
