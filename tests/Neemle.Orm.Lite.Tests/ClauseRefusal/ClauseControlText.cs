using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

internal static class ClauseControlText
{
    internal static string Expected(string provider, string entity, string operation)
    {
        var soft = entity == "soft";
        var table = soft ? "clause_soft" : "clause_plain";
        var live = soft ? "\"deleted_at\" IS NULL AND " : "";
        var where = $" WHERE {live}\"name\" = @p0";
        var now = ClauseWorld.ProviderOf(provider).CreateSerializer().Fragment(new ClockNow());
        var update = $"UPDATE \"{table}\" SET \"rank\" = @p1{where}";
        var delete = soft ? $"UPDATE \"{table}\" SET \"deleted_at\" = {now}{where}" : $"DELETE FROM \"{table}\"{where}";
        return operation switch
        {
            "UpdateAsync" => update,
            "UpdateReturningAsync" => update + " RETURNING \"rank\"",
            "DeleteAsync" => delete,
            "DeleteReturningAsync" => delete + " RETURNING \"rank\"",
            "PurgeAsync" => $"DELETE FROM \"{table}\" WHERE \"name\" = @p0",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "not a write operation"),
        };
    }
}
