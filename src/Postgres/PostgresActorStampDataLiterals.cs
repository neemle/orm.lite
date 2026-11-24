namespace Neemle.OrmLite.Postgres;

internal static class PostgresActorStampDataLiterals
{
    internal const string StatementHead = "SELECT set_config('";

    internal const string StatementTail = "', @v, false)";

    internal const string ValueParameter = "v";
}
