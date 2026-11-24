#nullable enable
using System;

namespace Neemle.OrmLite;

    // OnConflict.DoUpdate with no UpsertSet names no column to update, so it used to be written as DO NOTHING: the
    // caller asked for an update, got none, and on a soft-delete table a conflicting soft-deleted row was neither
    // updated nor reported (the live-row guard exists only when there is something to update). It is refused by name.
    internal static class UpsertSetGuard
    {
        internal static void RequireSets(OnConflict action, int setCount)
        {
            if (action != OnConflict.DoUpdate || setCount > 0) return;
            throw new InvalidOperationException(
                "OnConflict.DoUpdate names no UpsertSet, so it would update nothing and be written as a conflict that "
                + "is ignored, silently, and a soft-deleted row in the way would be neither updated nor reported. "
                + "Nothing was executed. Pass the sets to apply (UpsertSet.Inserted, Increment or Now), "
                + "or use OnConflict.DoNothing.");
        }
    }
