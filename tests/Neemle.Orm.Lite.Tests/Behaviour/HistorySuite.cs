using System.Runtime.CompilerServices;
using System.Text.Json;
using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public abstract class HistorySuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly ConditionalWeakTable<ProviderFixture, Task> Prepared = new();

    protected TFixture Fixture { get; } = fixture;

    protected OrmContext Ctx => Fixture.Context;

    protected async Task PrepareAsync()
    {
        Fixture.RequireAvailable();
        HistoryWorld.Register();
        await Prepared.GetValue(Fixture, _ => SetUpAsync());
    }

    private async Task SetUpAsync()
    {
        await MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, [.. HistoryWorld.Schema()]);
        await InstallAsync(HistoryWorld.Tables);
    }

    protected Task InstallAsync(IReadOnlyList<RowHistoryTable> tables) =>
        MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, [RowHistoryDdl.Install(tables)]);

    protected Query<HistTask> Tasks(UnitOfWork? work = null) => On(new Query<HistTask>(Ctx, HistTask.Map), work);

    protected Query<HistProject> Projects(UnitOfWork? work = null) => On(new Query<HistProject>(Ctx, HistProject.Map), work);

    protected Query<HistItem> Items(UnitOfWork? work = null) => On(new Query<HistItem>(Ctx, HistItem.Map), work);

    private static Query<T> On<T>(Query<T> query, UnitOfWork? work) where T : class => work is null ? query : query.On(work);

    protected async Task<Guid> InsertTaskAsync(Guid tenant, string name, int rank = 1, Guid? project = null, string? status = "open")
    {
        var id = Guid.NewGuid();
        await Tasks().InsertManyAsync([
            new Row<HistTask>().With(t => t.Id, id).With(t => t.TenantId, tenant).With(t => t.ProjectId, project)
                .With(t => t.Name, name).With(t => t.Rank, rank).With(t => t.Status, status),
        ]);
        return id;
    }

    protected async Task<Guid> InsertProjectAsync(Guid tenant, string name)
    {
        var id = Guid.NewGuid();
        await Projects().InsertManyAsync([
            new Row<HistProject>().With(p => p.Id, id).With(p => p.TenantId, tenant).With(p => p.Name, name).With(p => p.Status, "open"),
        ]);
        return id;
    }

    protected Task<IReadOnlyList<RowHistoryVersion>> VersionsAsync(string table, Guid id, Guid? tenant = null) =>
        RowHistoryRead.VersionsAsync(Ctx, table, id, tenant);

    protected async Task<int> StoredVersionsAsync(string table, Guid id)
    {
        var statement = HistoryStatements.Compile(
            Ctx.Provider, HistoryStatements.Versions(table, null, null, table, id, null));
        await using var cmd = Ctx.DataSource.CreateCommand(statement.Text);
        HistoryStatements.Bind(cmd, statement);
        await using var reader = await cmd.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync()) count++;
        return count;
    }

    protected static async Task AsAsync(Guid user, Func<Task> work)
    {
        using var scope = DbActor.EnterUser(user);
        await work();
    }

    protected Task RenameAsync(Guid id, string name) => Tasks().Where(t => t.Id == id).Set(t => t.Name, name).UpdateAsync();

    protected static string Text(JsonDocument state, string key) => state.RootElement.GetProperty(key).GetString()!;

    protected static int Number(JsonDocument state, string key) => state.RootElement.GetProperty(key).GetInt32();

    protected static string Pair(ISoftEntry entry, Guid id) => entry.Table + ":" + id;

    [SkippableFact(DisplayName = "an update is recorded with the values it replaced, the user who made it and the names of the versioned columns it changed, and a change to an unversioned column records nothing")]
    public async Task An_update_is_recorded_with_its_author()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var id = await InsertTaskAsync(tenant, "a");

        await AsAsync(user, () => Tasks().Where(t => t.Id == id).Set(t => t.Name, "b").Set(t => t.Rank, 2).UpdateAsync());
        await Tasks().Where(t => t.Id == id).Set(t => t.UpdatedAt, DateTime.UtcNow).UpdateAsync();

        var version = Assert.Single(await VersionsAsync(HistTask.Table, id, tenant));
        Assert.Equal("update", version.Op);
        Assert.Equal("user", version.ActorKind);
        Assert.Equal(user, version.ActorUserId);
        Assert.Equal(["name", "rank"], version.ChangedKeys);
        Assert.True(version.XactId > 0);
        Assert.InRange((DateTime.UtcNow - version.ChangedAt).TotalSeconds, -5, 120);
        Assert.Equal(DateTimeKind.Utc, version.ChangedAt.Kind);
    }

    [SkippableFact(DisplayName = "the author of a change is the user, the system or unknown according to the actor the caller entered, and the same on every provider")]
    public async Task Authors_are_attributed()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var id = await InsertTaskAsync(tenant, "a");

        await AsAsync(user, () => RenameAsync(id, "b"));
        using (DbActor.EnterSystem()) await RenameAsync(id, "c");
        await RenameAsync(id, "d");

        var versions = await VersionsAsync(HistTask.Table, id, tenant);
        Assert.Equal(["unknown", "system", "user"], versions.Select(v => v.ActorKind));
        Assert.Equal(new Guid?[] { null, null, user }, versions.Select(v => v.ActorUserId));
        Assert.True(versions[0].Id > versions[1].Id && versions[1].Id > versions[2].Id);
    }

    [SkippableFact(DisplayName = "a soft delete and a restore are recorded as their own operations on the soft column, and a hard delete removes the history of the row")]
    public async Task Soft_delete_restore_and_purge_are_recorded()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var id = await InsertTaskAsync(tenant, "a");

        await Tasks().Where(t => t.Id == id).DeleteAsync();
        await Tasks().RestoreAsync([id], tenant);

        var versions = await VersionsAsync(HistTask.Table, id, tenant);
        Assert.Equal(["restore", "soft_delete"], versions.Select(v => v.Op));
        Assert.All(versions, v => Assert.Equal(["deleted_at"], v.ChangedKeys));

        await Tasks().PurgeAsync([id], tenant);

        Assert.Equal(0, await StoredVersionsAsync(HistTask.Table, id));
        Assert.False(await RowHistoryRead.RowExistsAsync(Ctx, HistTask.Table, id, tenant));
    }

    [SkippableFact(DisplayName = "the existence of a row is read under its tenant, and a row of another tenant or a table with no key is refused or absent")]
    public async Task Row_existence_respects_the_tenant()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var id = await InsertTaskAsync(tenant, "a");

        Assert.True(await RowHistoryRead.RowExistsAsync(Ctx, HistTask.Table, id, tenant));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RowHistoryRead.RowExistsAsync(Ctx, HistTask.Table, id));
        Assert.False(await RowHistoryRead.RowExistsAsync(Ctx, HistTask.Table, id, Guid.NewGuid()));
        Assert.False(await RowHistoryRead.RowExistsAsync(Ctx, HistTask.Table, Guid.NewGuid(), tenant));
        await Assert.ThrowsAsync<ArgumentException>(() => RowHistoryRead.RowExistsAsync(Ctx, "hist_unknown", id));
    }

    [SkippableFact(DisplayName = "the state of a version is the row as it was before that change, and a version that does not belong to the row has none")]
    public async Task State_of_a_version_is_the_row_before_it()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var id = await InsertTaskAsync(tenant, "a", 1);
        await Tasks().Where(t => t.Id == id).Set(t => t.Name, "b").UpdateAsync();
        await Tasks().Where(t => t.Id == id).Set(t => t.Name, "c").Set(t => t.Rank, 2).UpdateAsync();
        var versions = await VersionsAsync(HistTask.Table, id, tenant);

        using var first = await RowHistoryRead.StateOfVersionAsync(Ctx, HistTask.Table, id, versions[1].Id, tenant);
        using var second = await RowHistoryRead.StateOfVersionAsync(Ctx, HistTask.Table, id, versions[0].Id, tenant);

        Assert.Equal(("a", 1), (Text(first!, "name"), Number(first!, "rank")));
        Assert.Equal(("b", 1), (Text(second!, "name"), Number(second!, "rank")));
        Assert.Equal(id.ToString(), Text(first!, "id"));
        Assert.False(first!.RootElement.TryGetProperty("updated_at", out _));
        Assert.Null(await RowHistoryRead.StateOfVersionAsync(Ctx, HistTask.Table, id, versions[0].Id + 1000, tenant));
        Assert.Null(await RowHistoryRead.StateOfVersionAsync(Ctx, HistTask.Table, id, versions[0].Id, Guid.NewGuid()));
    }

    [SkippableFact(DisplayName = "a revert puts back every column the later versions changed except the pinned and status ones, stamps the update time and records itself as a new version by the reverting user")]
    public async Task Revert_restores_an_earlier_state()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var entry = HistoryWorld.Entry<HistTask>();
        var project = Guid.NewGuid();
        var (id, first) = await TaskWithTwoUpdatesAsync(tenant, project);

        int affected;
        RevertRowLock? locked;
        bool exists;
        long newest;
        using (DbActor.EnterUser(user))
        {
            await using var work = await UnitOfWork.BeginAsync(Ctx);
            locked = await RowHistoryRevert.LockAsync(work, entry, id, tenant);
            exists = await RowHistoryRevert.VersionExistsAsync(work, entry, id, first.Id, tenant);
            newest = await RowHistoryRevert.NewestVersionAsync(work, entry, id, tenant);
            affected = await RowHistoryRevert.RevertAsync(work, entry, id, first.Id, tenant, includeStatus: false);
            await work.CommitAsync();
        }

        Assert.Equal(1, affected);
        Assert.False(locked!.Deleted);
        Assert.Equal(project, locked.ProjectId);
        Assert.True(exists);
        await AssertRowWasRevertedAsync(id, project);
        await AssertRevertWasRecordedAsync(id, tenant, newest, user);
    }

    private async Task<(Guid Id, RowHistoryVersion First)> TaskWithTwoUpdatesAsync(Guid tenant, Guid project)
    {
        var id = await InsertTaskAsync(tenant, "a", 1, project, "open");
        await Tasks().Where(t => t.Id == id).Set(t => t.Name, "b").Set(t => t.Status, "closed").UpdateAsync();
        await Tasks().Where(t => t.Id == id).Set(t => t.Name, "c").Set(t => t.Rank, 2).UpdateAsync();
        return (id, (await VersionsAsync(HistTask.Table, id, tenant))[^1]);
    }

    private async Task AssertRowWasRevertedAsync(Guid id, Guid project)
    {
        var row = Assert.Single(await Tasks().Where(t => t.Id == id).ToListAsync());
        Assert.Equal(("a", 1, "closed"), (row.Name, row.Rank, row.Status));
        Assert.Equal(project, row.ProjectId);
        Assert.NotNull(row.UpdatedAt);
    }

    private async Task AssertRevertWasRecordedAsync(Guid id, Guid tenant, long newest, Guid user)
    {
        var latest = (await VersionsAsync(HistTask.Table, id, tenant))[0];
        Assert.True(latest.Id > newest);
        Assert.Equal("update", latest.Op);
        Assert.Equal(user, latest.ActorUserId);
        Assert.Equal(["name", "rank"], latest.ChangedKeys);
    }

    [SkippableFact(DisplayName = "a revert that includes the status also puts back the status, and a revert of a deleted row, of an unknown version or of a missing row changes nothing")]
    public async Task Revert_covers_status_and_refuses_what_is_not_live()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var entry = HistoryWorld.Entry<HistTask>();
        var id = await InsertTaskAsync(tenant, "a", 1, null, "open");
        await Tasks().Where(t => t.Id == id).Set(t => t.Status, "closed").UpdateAsync();
        var first = (await VersionsAsync(HistTask.Table, id, tenant))[^1];

        await using (var work = await UnitOfWork.BeginAsync(Ctx))
        {
            Assert.Equal(1, await RowHistoryRevert.RevertAsync(work, entry, id, first.Id, tenant, includeStatus: true));
            Assert.Equal(0, await RowHistoryRevert.RevertAsync(work, entry, id, first.Id + 1000, tenant, includeStatus: true));
            Assert.Equal(0, await RowHistoryRevert.RevertAsync(work, entry, Guid.NewGuid(), first.Id, tenant, includeStatus: true));
            Assert.Null(await RowHistoryRevert.LockAsync(work, entry, Guid.NewGuid(), tenant));
            Assert.False(await RowHistoryRevert.VersionExistsAsync(work, entry, id, first.Id + 1000, tenant));
            await work.CommitAsync();
        }
        Assert.Equal("open", Assert.Single(await Tasks().Where(t => t.Id == id).ToListAsync()).Status);

        await Tasks().Where(t => t.Id == id).DeleteAsync();
        await using var again = await UnitOfWork.BeginAsync(Ctx);
        Assert.Equal(0, await RowHistoryRevert.RevertAsync(again, entry, id, first.Id, tenant, includeStatus: true));
        Assert.True((await RowHistoryRevert.LockAsync(again, entry, id, tenant))!.Deleted);
        var empty = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RowHistoryRevert.NewestVersionAsync(again, entry, empty, tenant));
    }

    [SkippableFact(DisplayName = "rows deleted together are planned and restored together, rows deleted apart are not, and the restore refuses a plan it cannot carry out whole")]
    public async Task A_batch_deleted_together_is_restored_together()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var projectEntry = HistoryWorld.Entry<HistProject>();
        var taskEntry = HistoryWorld.Entry<HistTask>();
        var project = await InsertProjectAsync(tenant, "p");
        var together1 = await InsertTaskAsync(tenant, "t1", 1, project);
        var together2 = await InsertTaskAsync(tenant, "t2", 2, project);
        var apart = await InsertTaskAsync(tenant, "t3", 3, project);
        await AsAsync(user, async () =>
        {
            await Tasks().Where(t => t.Id == apart).DeleteAsync();
            await using var work = await UnitOfWork.BeginAsync(Ctx);
            await Tasks(work).Where(t => t.Id == together1 || t.Id == together2).DeleteAsync();
            await Projects(work).Where(p => p.Id == project).DeleteAsync();
            await work.CommitAsync();
        });

        await using var restore = await UnitOfWork.BeginAsync(Ctx);
        var plan = await SoftRestore.PlanAsync(restore, projectEntry, project, tenant);

        Assert.Equal(SoftRestoreRootState.Deleted, plan.RootState);
        Assert.Equal(
            new[] { Pair(projectEntry, project), Pair(taskEntry, together1), Pair(taskEntry, together2) }.Order(StringComparer.Ordinal),
            plan.Rows.Select(r => Pair(r.Entry, r.Id)).Order(StringComparer.Ordinal));
        Assert.NotNull(plan.XactId);
        Assert.NotNull(plan.ChangedAt);
        Assert.All(plan.Rows.Where(r => r.Entry.Table == HistTask.Table), r => Assert.Equal(project, r.OwnerValues["project_id"]));
        Assert.All(plan.Rows, r => Assert.Equal(tenant, r.OwnerValues["tenant_id"]));

        Assert.Equal(3, await SoftRestore.ApplyAsync(Ctx, restore, plan, tenant));
        await restore.CommitAsync();

        Assert.Equal(1, await Projects().Where(p => p.Id == project).CountAsync());
        Assert.Equal(2, await Tasks().Where(t => t.Id == together1 || t.Id == together2).CountAsync());
        Assert.Equal(0, await Tasks().Where(t => t.Id == apart).CountAsync());
        Assert.Equal(1, await Tasks().IncludingDeleted().Where(t => t.Id == apart).CountAsync());
    }

    [SkippableFact(DisplayName = "a row deleted alone is planned alone, a live or missing root says so, and a plan whose rows changed meanwhile is refused with nothing committed")]
    public async Task A_root_is_planned_by_its_own_state()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var projectEntry = HistoryWorld.Entry<HistProject>();
        var taskEntry = HistoryWorld.Entry<HistTask>();
        var project = await InsertProjectAsync(tenant, "p");
        var alone = await InsertTaskAsync(tenant, "t1", 1, project);
        var live = await InsertTaskAsync(tenant, "t2", 1, project);
        await Tasks().Where(t => t.Id == alone).DeleteAsync();
        await Projects().Where(p => p.Id == project).DeleteAsync();

        await using var work = await UnitOfWork.BeginAsync(Ctx);
        var aloneOnly = await SoftRestore.PlanAsync(work, taskEntry, alone, tenant);
        var projectOnly = await SoftRestore.PlanAsync(work, projectEntry, project, tenant);
        var liveRoot = await SoftRestore.PlanAsync(work, taskEntry, live, tenant);
        var missing = await SoftRestore.PlanAsync(work, taskEntry, Guid.NewGuid(), tenant);

        Assert.Equal([Pair(taskEntry, alone)], aloneOnly.Rows.Select(r => Pair(r.Entry, r.Id)));
        Assert.Equal([Pair(projectEntry, project)], projectOnly.Rows.Select(r => Pair(r.Entry, r.Id)));
        Assert.Equal((SoftRestoreRootState.Live, 0), (liveRoot.RootState, liveRoot.Rows.Count));
        Assert.Equal((SoftRestoreRootState.Missing, 0), (missing.RootState, missing.Rows.Count));
        Assert.True(await SoftRestore.IsDeletedUnderShareLockAsync(work, taskEntry, alone, tenant));
        Assert.False(await SoftRestore.IsDeletedUnderShareLockAsync(work, taskEntry, live, tenant));
        Assert.Null(await SoftRestore.IsDeletedUnderShareLockAsync(work, taskEntry, Guid.NewGuid(), tenant));

        await Tasks(work).RestoreAsync([alone], tenant);
        var refusal = await Assert.ThrowsAsync<SoftRestoreException>(() => SoftRestore.ApplyAsync(Ctx, work, aloneOnly, tenant));
        Assert.Contains("hist_tasks", refusal.Message, StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "the trash lists deleted rows newest first with who deleted them and in which transaction, pages with a look-ahead row, filters by entity and project, and names the tables it skipped")]
    public async Task The_trash_lists_deleted_rows()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var project = await InsertProjectAsync(tenant, "pp");
        var a = await InsertTaskAsync(tenant, "a", 1, project);
        var b = await InsertTaskAsync(tenant, "b", 1, project);
        var c = await InsertTaskAsync(tenant, "c", 1, project);
        var d = await InsertTaskAsync(tenant, "d", 1, null);
        foreach (var id in new[] { a, b, c, d })
        {
            await AsAsync(user, () => Tasks().Where(t => t.Id == id).DeleteAsync());
            await Task.Delay(30);
        }
        var task = HistoryWorld.Entry<HistTask>();
        var ghost = new GhostEntry(task);
        var entries = new List<ISoftEntry> { task, HistoryWorld.Entry<HistProject>(), ghost };
        var only = new SoftTrashFilter(EntityToken: "hist_task");

        var all = await SoftTrashRead.ListAsync(Ctx, entries, tenant, only, 0, 10);
        var page = await SoftTrashRead.ListAsync(Ctx, entries, tenant, only, 1, 2);
        var byProject = await SoftTrashRead.ListAsync(Ctx, entries, tenant, new SoftTrashFilter(EntityToken: "hist_task", ProjectId: project), 0, 10);

        Assert.Equal([d, c, b, a], all.Rows.Select(r => r.Id));
        Assert.Equal(["d", "c", "b", "a"], all.Rows.Select(r => r.Label));
        Assert.Equal(new Guid?[] { null, project, project, project }, all.Rows.Select(r => r.ProjectId));
        Assert.All(all.Rows, r =>
        {
            Assert.Equal(("hist_task", HistTask.Table, "user"), (r.EntityToken, r.Table, r.ActorKind));
            Assert.Equal(user, r.ActorUserId);
        });
        Assert.All(all.Rows, r => Assert.True(r.XactId > 0 && r.ChangedAt is not null && r.DeletedAt.Kind == DateTimeKind.Utc));
        Assert.False(all.HasMore);
        Assert.Equal(["hist_ghosts"], all.SkippedTables);
        Assert.Equal([c, b], page.Rows.Select(r => r.Id));
        Assert.True(page.HasMore);
        Assert.Equal([c, b, a], byProject.Rows.Select(r => r.Id));

        var projects = await SoftTrashRead.ListAsync(Ctx, entries, tenant, new SoftTrashFilter(EntityToken: "project"), 0, 10);
        Assert.Empty(projects.Rows);
        await Projects().Where(p => p.Id == project).DeleteAsync();
        projects = await SoftTrashRead.ListAsync(Ctx, entries, tenant, new SoftTrashFilter(EntityToken: "project"), 0, 10);
        Assert.Equal([project], projects.Rows.Select(r => r.Id));
        Assert.Equal([project], projects.Rows.Select(r => r.ProjectId));
        Assert.Equal(["unknown"], projects.Rows.Select(r => r.ActorKind));
    }

    [SkippableFact(DisplayName = "platform entities are listed only when asked for, and a trash with nothing to list runs no listing statement")]
    public async Task The_trash_lists_platform_rows_on_request()
    {
        await PrepareAsync();
        var item = Guid.NewGuid();
        var code = "i" + item.ToString("N")[..8];
        await Items().InsertManyAsync([new Row<HistItem>().With(i => i.Id, item).With(i => i.Code, code)]);
        await Items().Where(i => i.Id == item).DeleteAsync();
        var entries = new List<ISoftEntry> { HistoryWorld.Entry<HistItem>() };
        var tenant = Guid.NewGuid();

        var without = await SoftTrashRead.ListAsync(Ctx, entries, tenant, new SoftTrashFilter(), 0, 5);
        var with = await SoftTrashRead.ListAsync(Ctx, entries, tenant, new SoftTrashFilter(IncludePlatform: true), 0, 500);

        Assert.Empty(without.Rows);
        Assert.Contains(with.Rows, r => r.Id == item && r.Label == code && r.ProjectId is null);
        Assert.Empty((await SoftTrashRead.ListAsync(Ctx, [], tenant, new SoftTrashFilter(), 0, 5)).Rows);
    }

    [SkippableFact(DisplayName = "the columns of a named unique index are read in key order, an unknown or empty name has none, and a null context is refused")]
    public async Task Constraint_columns_follow_the_index()
    {
        await PrepareAsync();

        var index = await ConstraintColumns.ForAsync(Ctx, "ix_hist_tasks_tenant_name");

        Assert.Equal("ix_hist_tasks_tenant_name", index.Name);
        Assert.Equal(["tenant_id", "name"], index.Columns);
        Assert.Empty((await ConstraintColumns.ForAsync(Ctx, "ix_nothing_at_all")).Columns);
        Assert.Empty((await ConstraintColumns.ForAsync(Ctx, "")).Columns);
        Assert.Empty((await ConstraintColumns.ForAsync(Ctx, null)).Columns);
        await Assert.ThrowsAsync<ArgumentNullException>(() => ConstraintColumns.ForAsync(null!, "x"));
    }

    [SkippableFact(DisplayName = "installing the history again changes nothing, and installing it for a different set of unversioned columns replaces what was installed")]
    public async Task The_install_is_reconciled_by_its_specification()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var id = await InsertTaskAsync(tenant, "a", 1, null, "open");
        await InstallAsync(HistoryWorld.Tables);
        await Tasks().Where(t => t.Id == id).Set(t => t.Status, "x").UpdateAsync();
        Assert.Single(await VersionsAsync(HistTask.Table, id, tenant));

        var wider = HistoryWorld.Tables.Select(t => t.Table == HistTask.Table ? t with { Unversioned = ["updated_at", "status"] } : t).ToList();
        try
        {
            await InstallAsync(wider);
            await Tasks().Where(t => t.Id == id).Set(t => t.Status, "y").UpdateAsync();
            Assert.Single(await VersionsAsync(HistTask.Table, id, tenant));
            await Tasks().Where(t => t.Id == id).Set(t => t.Name, "b").UpdateAsync();
            Assert.Equal(2, (await VersionsAsync(HistTask.Table, id, tenant)).Count);
        }
        finally
        {
            await InstallAsync(HistoryWorld.Tables);
        }

        await Tasks().Where(t => t.Id == id).Set(t => t.Status, "z").UpdateAsync();
        Assert.Equal(3, (await VersionsAsync(HistTask.Table, id, tenant)).Count);
        Assert.NotEqual(RowHistoryDdl.SetHash(HistoryWorld.Tables), RowHistoryDdl.SetHash(wider));
        Assert.Equal(RowHistoryDdl.SetHash(HistoryWorld.Tables), RowHistoryDdl.SetHash([.. HistoryWorld.Tables]));
    }

    [SkippableFact(DisplayName = "changes made in one unit of work share a transaction identity and a time, and changes made by separate statements do not")]
    public async Task A_unit_of_work_is_one_transaction()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var one = await InsertTaskAsync(tenant, "a");
        var two = await InsertTaskAsync(tenant, "b");
        await using (var work = await UnitOfWork.BeginAsync(Ctx))
        {
            await Tasks(work).Where(t => t.Id == one).Set(t => t.Name, "a2").UpdateAsync();
            await Task.Delay(20);
            await Tasks(work).Where(t => t.Id == two).Set(t => t.Name, "b2").UpdateAsync();
            await work.CommitAsync();
        }
        await Tasks().Where(t => t.Id == one).Set(t => t.Name, "a3").UpdateAsync();
        await Task.Delay(5);
        await Tasks().Where(t => t.Id == one).Set(t => t.Name, "a4").UpdateAsync();

        var together = new[] { (await VersionsAsync(HistTask.Table, one, tenant))[^1], (await VersionsAsync(HistTask.Table, two, tenant))[0] };
        var apart = (await VersionsAsync(HistTask.Table, one, tenant)).Take(2).ToList();

        Assert.Equal(together[0].XactId, together[1].XactId);
        Assert.Equal(together[0].ChangedAt, together[1].ChangedAt);
        Assert.NotEqual(apart[0].XactId, apart[1].XactId);
        Assert.NotEqual(apart[0].XactId, together[0].XactId);
        Assert.NotEqual(apart[0].ChangedAt, apart[1].ChangedAt);
    }

    [SkippableFact(DisplayName = "the history of a row moves with it: a revert, a restore and a soft delete by different users are read back in order with their own authors")]
    public async Task The_history_keeps_the_order_and_the_authors()
    {
        await PrepareAsync();
        var tenant = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var id = await InsertTaskAsync(tenant, "a");

        await AsAsync(first, () => RenameAsync(id, "b"));
        await AsAsync(second, () => Tasks().Where(t => t.Id == id).DeleteAsync());
        await AsAsync(first, () => Tasks().RestoreAsync([id], tenant));

        var versions = await VersionsAsync(HistTask.Table, id, tenant);
        Assert.Equal(["restore", "soft_delete", "update"], versions.Select(v => v.Op));
        Assert.Equal(new Guid?[] { first, second, first }, versions.Select(v => v.ActorUserId));
        Assert.Equal(versions.OrderByDescending(v => v.Id).Select(v => v.Id), versions.Select(v => v.Id));
    }

    private sealed class GhostEntry(ISoftEntry inner) : ISoftEntry
    {
        public Type ClrType => inner.ClrType;
        public string Table => "hist_ghosts";
        public string EntityToken => inner.EntityToken;
        public string KeyColumn => inner.KeyColumn;
        public string SoftColumn => inner.SoftColumn;
        public string? TenantColumn => inner.TenantColumn;
        public bool Platform => inner.Platform;
        public string? LabelColumn => inner.LabelColumn;
        public IReadOnlyList<string> Owners => inner.Owners;
        public IReadOnlyList<string> Unversioned => inner.Unversioned;
        public IReadOnlyList<string> Pinned => inner.Pinned;
        public IReadOnlyList<string> StatusColumns => inner.StatusColumns;
        public string? TypeColumnOfOwner(string ownerColumn) => inner.TypeColumnOfOwner(ownerColumn);
        public string? JsonNameOfColumn(string column) => inner.JsonNameOfColumn(column);
        public string? ColumnOfJsonName(string jsonName) => inner.ColumnOfJsonName(jsonName);
        public TResult Accept<TResult>(ISoftEntryVisitor<TResult> visitor) => inner.Accept(visitor);
    }
}

public sealed class SqliteHistoryTests(SqliteFixture fixture) : HistorySuite<SqliteFixture>(fixture);

public sealed class PostgresHistoryTests(PostgresFixture fixture) : HistorySuite<PostgresFixture>(fixture);
