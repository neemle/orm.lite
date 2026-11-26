using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class GraphSaveInsertCase
{
    internal static async Task RunAsync(OrmContext db, GraphSaveFixture f)
    {
        var permissions = GraphSavePermissions.None
            .Allow(GraphSaveProjectOrm.SaveRelations.Team,
                new(GraphSaveActions.Insert, GraphSaveKeyBinding.BindInWriteBuffer))
            .Allow(GraphSaveProjectOrm.SaveRelations.Tasks,
                new(GraphSaveActions.Insert, GraphSaveKeyBinding.BindInWriteBuffer))
            .Allow(GraphSaveProjectOrm.SaveRelations.Tags, new(GraphSaveActions.Insert | GraphSaveActions.AddLink));
        var intents = GraphSaveIntents.Empty.With(f.Team, GraphSaveIntent.Insert).With(f.First, GraphSaveIntent.Insert)
            .With(f.Sibling, GraphSaveIntent.Insert).With(f.Tag, GraphSaveIntent.Insert);
        var result = await GraphSave.SaveAsync(db, GraphSaveChecks.Request(f.Project, GraphSaveIntent.Insert,
            intents, permissions));
        GraphSaveChecks.Require(result.Outcome == GraphSaveOutcome.Committed, "acknowledged commit");
        GraphSaveChecks.Require(new[] { result.WasWritten(f.Team), result.WasWritten(f.Project),
            result.WasWritten(f.First), result.WasWritten(f.Sibling), result.WasWritten(f.Tag) }.All(v => v),
            "all writes");
        GraphSaveChecks.Require(f.Project.TeamId is null && f.First.ProjectId == Guid.Empty, "detached input FKs");
        var saved = await GraphSaveProjectOrm.GetAsync(db, f.Project.Id);
        GraphSaveChecks.Require(saved?.TeamId == f.Team.Id, "reference principal written before dependent");
        await f.RequireMembershipAsync(db);
        f.AdoptCommittedBindings(result);
        Console.WriteLine("graph-save/insert: reference, collection and unique Guid link committed; "
            + "input FKs unchanged");
    }
}
