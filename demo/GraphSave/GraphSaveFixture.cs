using Neemle.OrmLite.Generated;
using Neemle.OrmLite;

namespace OrmLiteDemo;

internal sealed class GraphSaveFixture
{
    internal GraphSaveTeam Team { get; } = new() { Id = GraphSaveChecks.Key(1), Name = "Engineering" };
    internal GraphSaveProject Project { get; } = new() { Id = GraphSaveChecks.Key(2), Title = "Release" };
    internal GraphSaveTask First { get; } = new() { Id = GraphSaveChecks.Key(300), Title = "Build" };
    internal GraphSaveTask Sibling { get; } = new() { Id = GraphSaveChecks.Key(400), Title = "Document" };
    internal GraphSaveTag Tag { get; } = new() { Id = GraphSaveChecks.Key(5), Name = "Priority" };

    internal GraphSaveFixture()
    {
        Project.Team = Team;
        Project.Tasks = [First, Sibling];
        Project.Tags = [Tag, Tag];
        Sibling.ProjectId = Project.Id;
    }

    internal void AdoptCommittedBindings(GraphSaveResult result)
    {
        Project.TeamId = result.BoundForeignKey<GraphSaveProject, Guid>(Project, nameof(GraphSaveProject.TeamId));
        First.ProjectId = result.BoundForeignKey<GraphSaveTask, Guid>(First, nameof(GraphSaveTask.ProjectId));
    }

    internal async Task RequireMembershipAsync(OrmContext db)
    {
        var tasks = await GraphSaveTaskOrm.Query(db).Where(t => t.ProjectId == Project.Id).ToListAsync();
        GraphSaveChecks.Require(tasks.Select(t => t.Id).ToHashSet().SetEquals([First.Id, Sibling.Id]),
            "task membership");
        var loaded = new GraphSaveProject { Id = Project.Id };
        await using var connection = await db.DataSource.OpenConnectionAsync();
        await GraphSaveProjectOrm.LoadTagsAsync(db, connection, loaded);
        GraphSaveChecks.Require(loaded.Tags.Count == 1 && loaded.Tags[0].Id == Tag.Id, "one exact tag pair");
    }
}
