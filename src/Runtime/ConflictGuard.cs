#nullable enable
using System;
using System.Collections.Generic;

namespace Neemle.OrmLite;

    internal static class ConflictGuard
    {
        internal static void RequireDefined(OnConflict action)
        {
            if (!Enum.IsDefined(action))
                throw new ArgumentOutOfRangeException(
                    nameof(action), (int)action,
                    "OnConflict has no value " + (int)action + "; use OnConflict.DoNothing or OnConflict.DoUpdate.");
        }

        internal static void RequireTarget(IReadOnlyList<string> conflictColumns, OnConflict action)
        {
            if (conflictColumns.Count == 0 && action == OnConflict.DoUpdate)
                throw new InvalidOperationException(
                    "an untargeted ON CONFLICT cannot DO UPDATE; PostgreSQL requires a conflict target to "
                    + "know which row to update. Name the columns, or use OnConflict.DoNothing.");
        }
    }
