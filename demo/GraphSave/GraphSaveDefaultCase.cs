using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class GraphSaveDefaultCase
{
    internal static async Task RunAsync(OrmContext db)
    {
        var child = new GraphSaveTask { Id = GraphSaveChecks.Key(71), Title = "Disabled task" };
        var root = new GraphSaveProject { Id = GraphSaveChecks.Key(70), Title = "Root only",
            Team = new() { Id = GraphSaveChecks.Key(72), Name = "Disabled team" }, Tasks = [child],
            Tags = [new() { Id = GraphSaveChecks.Key(73), Name = "Disabled tag" }] };
        var result = await GraphSave.SaveAsync(db, GraphSaveChecks.Request(root, GraphSaveIntent.Insert,
            GraphSaveIntents.Empty, GraphSavePermissions.None));
        var saved = await GraphSaveProjectOrm.GetAsync(db, root.Id);
        GraphSaveChecks.Require(result.WasWritten(root) && !result.WasWritten(child), "root-only result");
        GraphSaveChecks.Require(saved is { TeamId: null } && child.ProjectId == Guid.Empty, "disabled FK untouched");
        GraphSaveChecks.Require(await GraphSaveTaskOrm.Query(db).CountAsync() == 0, "disabled tasks absent");
        GraphSaveChecks.Require(await GraphSaveTagOrm.Query(db).CountAsync() == 0, "disabled tags absent");
        GraphSaveChecks.Require(await GraphSaveTeamOrm.Query(db).CountAsync() == 0, "disabled team absent");
        Console.WriteLine("graph-save/default-off: root only; populated navigations untouched");
    }
}
