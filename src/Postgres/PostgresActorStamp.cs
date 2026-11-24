using System.Data.Common;

namespace Neemle.OrmLite.Postgres;

// The PostgreSQL spelling of the actor stamp: one custom setting of the session carries the acting user, so a
// trigger can read it back. The core only owns the validated setting name; the statement text is written here.
internal static class PostgresActorStamp
{
    internal static string Statement =>
        PostgresActorStampDataLiterals.StatementHead + OrmLiteSettings.ActorSetting
        + PostgresActorStampDataLiterals.StatementTail;

    internal static async Task StampAsync(DbConnection connection, DbActorValue actor, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Statement;
        command.AddParam(PostgresActorStampDataLiterals.ValueParameter, actor.Stamp);
        await command.ExecuteNonQueryAsync(ct);
    }
}
