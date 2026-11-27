namespace Neemle.OrmLite.Postgres;

internal static class PostgresOrphanPredicates
{
    internal static bool Matches(string expression, int ordinal, long type, int modifier, long collation)
    {
        if (ordinal <= 0 || type <= 0 || modifier < -1 || collation < 0) return false;
        var identity = FormattableString.Invariant(
            $":varattno {ordinal} :vartype {type} :vartypmod {modifier} ");
        var flags = FormattableString.Invariant(
            $":varcollid {collation} :varnullingrels (b) :varlevelsup 0 ");
        var syntax = FormattableString.Invariant(
            $":varreturningtype 0 :varnosyn 1 :varattnosyn {ordinal} :location -1}}");
        var variable = "{VAR :varno 1 " + identity + flags + syntax;
        return expression == "{NULLTEST :arg " + variable
            + " :nulltesttype 0 :argisrow false :location -1}";
    }
}
