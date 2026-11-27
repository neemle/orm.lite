using System.Globalization;
using L = Neemle.OrmLite.Postgres.PostgresOrphanDataLiterals;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresOrphanPredicates
{
    internal static bool Matches(string expression, int ordinal, long type, int modifier, long collation)
    {
        if (ordinal <= 0 || type <= 0 || modifier < -1 || collation < 0) return false;
        return expression == string.Format(CultureInfo.InvariantCulture, L.LiveNullTest,
            ordinal, type, modifier, collation);
    }
}
