using System.Runtime.CompilerServices;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

// One tenant rule for the history reads, the revert and the restore, run against a real database: a tenant row
// (HistTask), a tenant-root row whose tenant column is its key (HistRoot) and a Platform row (HistItem), each with
// two tenants whose rows have the same shape. A wrong tenant finds nothing and changes nothing; a missing tenant
// is refused.
public abstract class HistoryScopeSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly ConditionalWeakTable<ProviderFixture, Task> Prepared = new();

    private OrmContext Ctx => fixture.Context;

    private async Task PrepareAsync()
    {
        fixture.RequireAvailable();
        HistoryWorld.Register();
        HistoryRootWorld.Register();
        await Prepared.GetValue(fixture, _ => SetUpAsync());
    }

    private async Task SetUpAsync()
    {
        var schema = new List<Node>(HistoryWorld.Schema()) { HistoryRootWorld.Schema() };
        await MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, schema);
        var tables = new List<RowHistoryTable>(HistoryWorld.Tables);
        tables.AddRange(HistoryRootWorld.Tables);
        await MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, [RowHistoryDdl.Install(tables)]);
    }

    private static Query<T> Over<T>(Query<T> query, UnitOfWork? work) where T : class =>
        work is null ? query : query.On(work);

    private Query<HistRoot> Roots(UnitOfWork? work = null) => Over(new Query<HistRoot>(Ctx, HistRoot.Map), work);

    private Query<HistTask> Tasks() => new(Ctx, HistTask.Map);

    private Query<HistItem> Items() => new(Ctx, HistItem.Map);

    private async Task<Guid> InsertRootAsync(string name)
    {
        var id = Guid.NewGuid();
        await Roots().InsertManyAsync([
            new Row<HistRoot>().With(r => r.Id, id).With(r => r.Name, name).With(r => r.Status, "open"),
        ]);
        return id;
    }

    private async Task<Guid> InsertTaskAsync(Guid tenant, string name)
    {
        var id = Guid.NewGuid();
        await Tasks().InsertManyAsync([
            new Row<HistTask>().With(t => t.Id, id).With(t => t.TenantId, tenant).With(t => t.ProjectId, (Guid?)null)
                .With(t => t.Name, name).With(t => t.Rank, 1).With(t => t.Status, "open"),
        ]);
        return id;
    }

    private async Task<(Guid A, Guid B)> TwoRootsAsync()
    {
        var a = await InsertRootAsync("same");
        var b = await InsertRootAsync("same");
        foreach (var id in new[] { a, b })
        {
            await Roots().Where(r => r.Id == id).Set(r => r.Name, "next").UpdateAsync();
            await Roots().Where(r => r.Id == id).Set(r => r.Name, "last").UpdateAsync();
        }
        return (a, b);
    }

    [SkippableFact(DisplayName = "the history of a tenant-root row is read only under its own tenant, "
        + "and a read with no tenant is refused")]
    public async Task A_tenant_root_row_is_read_only_under_its_own_tenant()
    {
        await PrepareAsync();
        var (a, b) = await TwoRootsAsync();
        var versions = await RowHistoryRead.VersionsAsync(Ctx, HistRoot.Table, a, a);

        using var own = await RowHistoryRead.StateOfVersionAsync(Ctx, HistRoot.Table, a, versions[0].Id, a);

        Assert.Equal(2, versions.Count);
        Assert.NotNull(own);
        Assert.Empty(await RowHistoryRead.VersionsAsync(Ctx, HistRoot.Table, a, b));
        Assert.True(await RowHistoryRead.RowExistsAsync(Ctx, HistRoot.Table, a, a));
        Assert.False(await RowHistoryRead.RowExistsAsync(Ctx, HistRoot.Table, a, b));
        Assert.Null(await RowHistoryRead.StateOfVersionAsync(Ctx, HistRoot.Table, a, versions[0].Id, b));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RowHistoryRead.VersionsAsync(Ctx, HistRoot.Table, a));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RowHistoryRead.RowExistsAsync(Ctx, HistRoot.Table, a));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RowHistoryRead.StateOfVersionAsync(Ctx, HistRoot.Table, a, versions[0].Id));
    }

    [SkippableFact(DisplayName = "a wrong tenant cannot lock, probe or revert a tenant-root row, "
        + "and the row is left as it was")]
    public async Task A_wrong_tenant_cannot_lock_probe_or_revert_a_tenant_root_row()
    {
        await PrepareAsync();
        var (a, b) = await TwoRootsAsync();
        var entry = HistoryWorld.Entry<HistRoot>();
        var first = (await RowHistoryRead.VersionsAsync(Ctx, HistRoot.Table, a, a))[^1];
        await using var work = await UnitOfWork.BeginAsync(Ctx);

        Assert.Null(await RowHistoryRevert.LockAsync(work, entry, a, b));
        Assert.False(await RowHistoryRevert.VersionExistsAsync(work, entry, a, first.Id, b));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RowHistoryRevert.NewestVersionAsync(work, entry, a, b));
        Assert.Equal(0, await RowHistoryRevert.RevertAsync(work, entry, a, first.Id, b, includeStatus: false));

        Assert.Equal("last", Assert.Single(await Roots(work).Where(r => r.Id == a).ToListAsync()).Name);
    }

    [SkippableFact(DisplayName = "the tenant of a tenant-root row locks, finds the version and reverts it, so the "
        + "refusals are the scope and not a broken statement")]
    public async Task The_own_tenant_reverts_a_tenant_root_row()
    {
        await PrepareAsync();
        var (a, _) = await TwoRootsAsync();
        var entry = HistoryWorld.Entry<HistRoot>();
        var first = (await RowHistoryRead.VersionsAsync(Ctx, HistRoot.Table, a, a))[^1];
        await using var work = await UnitOfWork.BeginAsync(Ctx);

        Assert.NotNull(await RowHistoryRevert.LockAsync(work, entry, a, a));
        Assert.True(await RowHistoryRevert.VersionExistsAsync(work, entry, a, first.Id, a));
        Assert.True(await RowHistoryRevert.NewestVersionAsync(work, entry, a, a) >= first.Id);
        Assert.Equal(1, await RowHistoryRevert.RevertAsync(work, entry, a, first.Id, a, includeStatus: false));

        Assert.Equal("same", Assert.Single(await Roots(work).Where(r => r.Id == a).ToListAsync()).Name);
    }

    [SkippableFact(DisplayName = "a wrong tenant plans no restore of a deleted tenant-root row and sees it as "
        + "missing, its own tenant plans it")]
    public async Task A_wrong_tenant_plans_no_restore_of_a_tenant_root_row()
    {
        await PrepareAsync();
        var (a, b) = await TwoRootsAsync();
        await Roots().Where(r => r.Id == a).DeleteAsync();
        var entry = HistoryWorld.Entry<HistRoot>();
        await using var work = await UnitOfWork.BeginAsync(Ctx);

        var wrong = await SoftRestore.PlanAsync(work, entry, a, b);
        var own = await SoftRestore.PlanAsync(work, entry, a, a);

        Assert.Equal((SoftRestoreRootState.Missing, 0), (wrong.RootState, wrong.Rows.Count));
        Assert.Equal((SoftRestoreRootState.Deleted, 1), (own.RootState, own.Rows.Count));
        Assert.Null(await SoftRestore.IsDeletedUnderShareLockAsync(work, entry, a, b));
        Assert.True(await SoftRestore.IsDeletedUnderShareLockAsync(work, entry, a, a));
    }

    [SkippableFact(DisplayName = "a wrong tenant cannot probe the version reads of a tenant row or plan its "
        + "restore, and a read with no tenant is refused")]
    public async Task A_wrong_tenant_cannot_probe_the_version_reads_of_a_tenant_row()
    {
        await PrepareAsync();
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var a = await InsertTaskAsync(tenantA, "same");
        var b = await InsertTaskAsync(tenantB, "same");
        foreach (var id in new[] { a, b })
            await Tasks().Where(t => t.Id == id).Set(t => t.Name, "next").UpdateAsync();
        await Tasks().Where(t => t.Id == a).DeleteAsync();
        var entry = HistoryWorld.Entry<HistTask>();
        var first = (await RowHistoryRead.VersionsAsync(Ctx, HistTask.Table, a, tenantA))[^1];
        await using var work = await UnitOfWork.BeginAsync(Ctx);

        Assert.False(await RowHistoryRevert.VersionExistsAsync(work, entry, a, first.Id, tenantB));
        Assert.True(await RowHistoryRevert.VersionExistsAsync(work, entry, a, first.Id, tenantA));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RowHistoryRevert.NewestVersionAsync(work, entry, a, tenantB));
        Assert.True(await RowHistoryRevert.NewestVersionAsync(work, entry, a, tenantA) >= first.Id);
        var wrong = await SoftRestore.PlanAsync(work, entry, a, tenantB);
        var own = await SoftRestore.PlanAsync(work, entry, a, tenantA);
        Assert.Equal((SoftRestoreRootState.Missing, SoftRestoreRootState.Deleted), (wrong.RootState, own.RootState));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RowHistoryRead.VersionsAsync(Ctx, HistTask.Table, a));
    }

    [SkippableFact(DisplayName = "a Platform row needs no tenant and ignores one: its history, lock and version "
        + "reads answer the same under any")]
    public async Task A_platform_row_ignores_the_tenant()
    {
        await PrepareAsync();
        var item = Guid.NewGuid();
        var code = "p" + item.ToString("N")[..8];
        await Items().InsertManyAsync([new Row<HistItem>().With(i => i.Id, item).With(i => i.Code, code)]);
        await Items().Where(i => i.Id == item).Set(i => i.Code, "changed").UpdateAsync();
        var entry = HistoryWorld.Entry<HistItem>();
        var other = Guid.NewGuid();
        var version = Assert.Single(await RowHistoryRead.VersionsAsync(Ctx, HistItem.Table, item));
        await using var work = await UnitOfWork.BeginAsync(Ctx);

        Assert.Single(await RowHistoryRead.VersionsAsync(Ctx, HistItem.Table, item, other));
        Assert.NotNull(await RowHistoryRevert.LockAsync(work, entry, item, other));
        Assert.True(await RowHistoryRevert.VersionExistsAsync(work, entry, item, version.Id, other));
        Assert.True(await RowHistoryRevert.NewestVersionAsync(work, entry, item, other) >= version.Id);
    }
}
