using Xunit;

namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

[Collection("Graph")]
public sealed class OwnerCleanupTests
{
    [Theory(DisplayName = "[UC-004][AC07/08] policy close failure drains then attempts every unit resource once")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unit_close_fault_keeps_primary_and_releases_every_owner_stage(bool callback)
    {
        var plan = new OwnerFaultPlan
            { ThrowCallback = callback, ThrowUnsubscribe = !callback, FailResources = true };
        var source = new OwnerSource(plan);
        var context = new OrmContext(source, new OwnerProvider(plan));
        var unit = await UnitOfWork.BeginAsync(context);
        var connection = unit.Connection;
        Assert.True(UnitOfWork.IsOpenOn(connection));
        var scope = LoadingScope.Bind(context, unit, OwnerModel.Options);
        var work = new OwnerAdmittedWork(scope, plan);
        var running = work.Start();
        await work.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var closing = unit.DisposeAsync().AsTask();
        OwnerAssertions.BeforeDrain(plan, closing);
        work.Release.TrySetResult();
        Assert.Equal(RelationScopeError.Closed, (await running.WaitAsync(TimeSpan.FromSeconds(10))).Error);
        var primary = await OwnerAssertions.ClosedAsync(closing, scope);
        UnitResults(plan, primary, unit, connection);
        await OwnerAssertions.FencedAsync(scope, work, plan);
        await unit.DisposeAsync();
        Assert.Equal(1, plan.Events.Count(e => e == "transaction"));
        Assert.Equal(1, plan.Events.Count(e => e == "connection"));
        Assert.Equal(1, plan.Events.Count(e => e == "release"));
    }

    private static void UnitResults(OwnerFaultPlan plan, Exception primary, UnitOfWork unit,
        System.Data.Common.DbConnection connection)
    {
        Assert.Equal(["cancel", "drained", "unsubscribe", "transaction", "connection", "release"], plan.Events);
        Assert.False(UnitOfWork.IsOpenOn(connection));
        Assert.Throws<ObjectDisposedException>(() => _ = unit.Connection);
        Assert.Throws<ObjectDisposedException>(() => _ = unit.Transaction);
        Assert.False(unit.Committed);
        OwnerAssertions.OriginalFailure(primary, plan);
        OwnerAssertions.PrivateSecondaries(primary, 3);
    }

    [Fact(DisplayName = "[UC-004][AC07/08][SEC] context attempts all owned sources after scope drain/close failure")]
    public async Task Context_close_fault_keeps_all_owned_cleanup_and_private_bounded_errors()
    {
        var plan = new OwnerFaultPlan { ThrowUnsubscribe = true, FailResources = true };
        var owned = Enumerable.Range(0, 8).Select(i => new OwnerSource(plan, "owned" + i)).ToArray();
        var context = new OrmContext(owned[0], new OwnerProvider(plan), owned);
        var scope = await LoadingScope.OpenAsync(context, OwnerModel.Options);
        var work = new OwnerAdmittedWork(scope, plan);
        var running = work.Start();
        await work.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var closing = context.DisposeAsync().AsTask();
        OwnerAssertions.BeforeDrain(plan, closing);
        work.Release.TrySetResult();
        Assert.Equal(RelationScopeError.Closed, (await running.WaitAsync(TimeSpan.FromSeconds(10))).Error);
        var primary = await OwnerAssertions.ClosedAsync(closing, scope);
        var expected = new[] { "cancel", "drained", "unsubscribe", "close", "connection" }
            .Concat(Enumerable.Range(0, 8).Select(i => "owned" + i));
        Assert.Equal(expected, plan.Events);
        Assert.True(context.PolicyLifetime.IsClosed);
        OwnerAssertions.OriginalFailure(primary, plan);
        OwnerAssertions.PrivateSecondaries(primary, 4);
        await OwnerAssertions.FencedAsync(scope, work, plan);
    }
}
