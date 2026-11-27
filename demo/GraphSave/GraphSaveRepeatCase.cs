using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class GraphSaveRepeatCase
{
    internal static async Task RunAsync(OrmContext db, GraphSaveFixture f)
    {
        f.Project.Title = "Release updated";
        f.First.Title = "Build updated";
        f.Project.Tasks = [f.First];
        f.Project.Tags = [f.Tag];
        var permissions = GraphSavePermissions.None
            .Allow(GraphSaveProjectOrm.SaveRelations.Team, new(GraphSaveActions.None))
            .Allow(GraphSaveProjectOrm.SaveRelations.Tasks, new(GraphSaveActions.Update))
            .Allow(GraphSaveProjectOrm.SaveRelations.Tags, new(GraphSaveActions.AddLink));
        var intents = GraphSaveIntents.Empty.With(f.Team, GraphSaveIntent.Unchanged)
            .With(f.First, GraphSaveIntent.Update).With(f.Tag, GraphSaveIntent.Unchanged);
        var request = GraphSaveChecks.Request(f.Project, GraphSaveIntent.Update, intents, permissions);
        await GraphSave.SaveAsync(db, request);
        var repeated = await GraphSave.SaveAsync(db, request);
        GraphSaveChecks.Require(repeated.WasWritten(f.Project) && repeated.WasWritten(f.First)
            && !repeated.WasWritten(f.Team) && !repeated.WasWritten(f.Tag), "explicit mutation intent only");
        var sibling = await GraphSaveTaskOrm.GetAsync(db, f.Sibling.Id);
        var updated = await GraphSaveTaskOrm.GetAsync(db, f.First.Id);
        GraphSaveChecks.Require(sibling?.Title == "Document" && updated?.Title == "Build updated",
            "only intended update");
        f.Project.Tasks = [];
        f.Project.Tags = [];
        await GraphSave.SaveAsync(db, GraphSaveChecks.Request(f.Project, GraphSaveIntent.Update,
            GraphSaveIntents.Empty, GraphSavePermissions.None));
        await f.RequireMembershipAsync(db);
        Console.WriteLine("graph-save/repeat: unchanged anchors, explicit updates, one link; "
            + "omitted sibling/link preserved");
    }
}
