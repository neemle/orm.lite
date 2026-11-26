using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal static class OwnerAssertions
{
    internal static void BeforeDrain(OwnerFaultPlan plan, Task closing)
    {
        Assert.False(closing.IsCompleted);
        Assert.Equal(["cancel"], plan.Events);
        Assert.Equal(0, plan.Commands);
        Assert.Equal(1, plan.Opens);
    }

    internal static async Task<Exception> ClosedAsync(Task closing, LoadingScope scope)
    {
        var primary = await Assert.ThrowsAnyAsync<Exception>(() => closing.WaitAsync(TimeSpan.FromSeconds(10)));
        var repeated = await Assert.ThrowsAnyAsync<Exception>(() => scope.DisposeAsync().AsTask());
        Assert.Same(primary, repeated);
        Assert.NotNull(primary.StackTrace);
        return primary;
    }

    internal static async Task FencedAsync(LoadingScope scope, OwnerAdmittedWork work, OwnerFaultPlan plan)
    {
        Assert.Same(work.OldValue, work.Row.Next);
        Assert.False(scope.State(work.Row, OwnerModel.Path).IsAttached);
        var error = await Assert.ThrowsAsync<RelationScopeException>(() => scope.LoadAsync(work.Row, OwnerModel.Path));
        Assert.Equal(RelationScopeError.Closed, error.Error);
        Assert.Equal(0, plan.Commands);
        Assert.Equal(1, plan.Opens);
    }

    internal static void OriginalFailure(Exception primary, OwnerFaultPlan plan)
    {
        if (plan.ThrowCallback)
        {
            var aggregate = Assert.IsType<AggregateException>(primary);
            Assert.Same(plan.CloseFailure, Assert.Single(aggregate.Flatten().InnerExceptions));
            Assert.Contains("OwnerAdmittedWork.Cancel", plan.CloseFailure.StackTrace);
        }
        else
        {
            Assert.Same(plan.CloseFailure, primary);
            Assert.Contains("OwnerConnection.remove_StateChange", primary.StackTrace);
        }
        Assert.Empty(primary.Data);
        Assert.DoesNotContain("private-secondary-password", primary.ToString());
    }

    internal static void PrivateSecondaries(Exception primary, int count)
    {
        var table = Assert.IsType<ConditionalWeakTable<Exception, PolicySecondaryErrors>>(typeof(PolicyCleanup)
            .GetField("Secondary", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        Assert.True(table.TryGetValue(primary, out var retained));
        var errors = Assert.IsType<List<Exception>>(typeof(PolicySecondaryErrors)
            .GetField("_errors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(retained));
        Assert.Equal(count, errors.Count);
        Assert.All(errors, error => Assert.NotSame(primary, error));
    }
}
