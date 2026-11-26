using Microsoft.Data.Sqlite;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Npgsql;

namespace OrmLiteDemo;

internal static class GraphSaveRollbackCase
{
    internal static async Task RunAsync(OrmContext db, GraphSaveFixture f)
    {
        var root = new GraphSaveProject { Id = GraphSaveChecks.Key(900), TeamId = f.Team.Id, Title = "Rollback" };
        var first = new GraphSaveTask { Id = GraphSaveChecks.Key(100), ProjectId = root.Id, Title = "Prefix" };
        var duplicate = new GraphSaveTask { Id = f.First.Id, ProjectId = root.Id, Title = "Conflict" };
        root.Tasks = [first, duplicate];
        var permissions = GraphSavePermissions.None.Allow(GraphSaveProjectOrm.SaveRelations.Tasks,
            new(GraphSaveActions.Insert));
        var intents = GraphSaveIntents.Empty.With(first, GraphSaveIntent.Insert)
            .With(duplicate, GraphSaveIntent.Insert);
        using var commands = new GraphSaveCommandCounter();
        try
        {
            await GraphSave.SaveAsync(db, GraphSaveChecks.Request(root, GraphSaveIntent.Insert, intents, permissions));
        }
        catch (GraphSaveException error) when (error.Outcome == GraphSaveOutcome.RolledBack && TaskConflict(error))
        {
            GraphSaveChecks.Require(commands.Attempted == 3, "two successful writes before third-statement failure");
            GraphSaveChecks.Require(await GraphSaveProjectOrm.GetAsync(db, root.Id) is null, "root prefix rolled back");
            GraphSaveChecks.Require(await GraphSaveTaskOrm.GetAsync(db, first.Id) is null, "task prefix rolled back");
            var preserved = await GraphSaveTaskOrm.GetAsync(db, f.First.Id);
            GraphSaveChecks.Require(preserved?.Title == "Build updated", "existing conflicting row preserved");
            GraphSaveChecks.Require(root.Tasks.Count == 2 && first.ProjectId == root.Id, "caller graph untouched");
            Console.WriteLine("graph-save/rollback: two writes succeeded before conflict; "
                + "no committed subset or input mutation");
            return;
        }
        throw new InvalidOperationException("Expected task conflict and acknowledged rollback");
    }

    private static bool TaskConflict(GraphSaveException error) => error.InnerException switch
    {
        SqliteException sqlite => sqlite.SqliteErrorCode == 19 && sqlite.Message.Contains("gs_demo_tasks.id"),
        PostgresException postgres => postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.TableName == "gs_demo_tasks",
        _ => false,
    };
}
