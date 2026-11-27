using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class GraphSaveRefusalCase
{
    internal static async Task RunAsync(OrmContext db, GraphSaveFixture f)
    {
        var child = new GraphSaveTask { Id = GraphSaveChecks.Key(81), ProjectId = f.Project.Id, Title = "No update" };
        var root = new GraphSaveProject { Id = GraphSaveChecks.Key(80), Title = "Refused", Tasks = [child] };
        var permissions = GraphSavePermissions.None.Allow(GraphSaveProjectOrm.SaveRelations.Tasks,
            new(GraphSaveActions.Insert));
        var request = GraphSaveChecks.Request(root, GraphSaveIntent.Insert,
            GraphSaveIntents.Empty.With(child, GraphSaveIntent.Update), permissions);
        await GraphSaveChecks.RefuseAsync(() => GraphSave.SaveAsync(db, request), GraphSaveError.ActionNotAllowed,
            GraphSaveOutcome.NotStarted);
        await MissingAnchorAsync(db);
        await ForeignKeyAsync(db);
        var partial = (await GraphSaveProjectOrm.Query(db).Where(p => p.Id == f.Project.Id)
            .ProjectAsync([p => p.Id], r => new GraphSaveProject { Id = r.GetGuid(0), Title = "Partial" })).Single();
        await GraphSaveChecks.RefuseAsync(() => GraphSave.SaveAsync(db, GraphSaveChecks.Request(partial,
            GraphSaveIntent.Update, GraphSaveIntents.Empty, GraphSavePermissions.None)),
            GraphSaveError.UnsupportedMapping, GraphSaveOutcome.NotStarted);
        await CanceledAsync(db, root);
        GraphSaveChecks.Require(await GraphSaveProjectOrm.GetAsync(db, root.Id) is null, "refused root absent");
        Console.WriteLine("graph-save/refusal: action, anchor, FK, partial origin and pre-cancellation stay unwritten");
    }

    private static async Task MissingAnchorAsync(OrmContext db)
    {
        var team = new GraphSaveTeam { Id = GraphSaveChecks.Key(83), Name = "Missing" };
        var root = new GraphSaveProject { Id = GraphSaveChecks.Key(82), TeamId = team.Id,
            Title = "No anchor", Team = team };
        var request = GraphSaveChecks.Request(root, GraphSaveIntent.Insert,
            GraphSaveIntents.Empty.With(team, GraphSaveIntent.Unchanged),
            GraphSavePermissions.None.Allow(GraphSaveProjectOrm.SaveRelations.Team, new(GraphSaveActions.None)));
        await GraphSaveChecks.RefuseAsync(() => GraphSave.SaveAsync(db, request), GraphSaveError.MissingAnchor,
            GraphSaveOutcome.RolledBack);
        GraphSaveChecks.Require(await GraphSaveProjectOrm.GetAsync(db, root.Id) is null, "missing-anchor root absent");
    }

    private static async Task ForeignKeyAsync(OrmContext db)
    {
        var root = new GraphSaveProject { Id = GraphSaveChecks.Key(84), TeamId = GraphSaveChecks.Key(999),
            Title = "Unresolved physical FK" };
        try
        {
            await GraphSave.SaveAsync(db, GraphSaveChecks.Request(root, GraphSaveIntent.Insert,
                GraphSaveIntents.Empty, GraphSavePermissions.None));
        }
        catch (GraphSaveException failure) when (failure.Outcome == GraphSaveOutcome.RolledBack
            && ForeignKeyFailure(failure.InnerException))
        {
            GraphSaveChecks.Require(await GraphSaveProjectOrm.GetAsync(db, root.Id) is null, "FK constraint rollback");
            return;
        }
        throw new InvalidOperationException("Expected enabled database FK enforcement");
    }

    private static bool ForeignKeyFailure(Exception? cause) => cause switch
    {
        Microsoft.Data.Sqlite.SqliteException sqlite => sqlite.SqliteExtendedErrorCode == 787,
        Npgsql.PostgresException postgres => postgres.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation,
        _ => false,
    };

    private static async Task CanceledAsync(OrmContext db, GraphSaveProject root)
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        try
        {
            await GraphSave.SaveAsync(db, GraphSaveChecks.Request(root, GraphSaveIntent.Insert,
                GraphSaveIntents.Empty, GraphSavePermissions.None), canceled.Token);
        }
        catch (GraphSaveCanceledException error) when (error.Outcome == GraphSaveOutcome.NotStarted) { return; }
        throw new InvalidOperationException("Expected pre-admission cancellation");
    }
}
