namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal sealed class OwnerAdmittedWork(LoadingScope scope, OwnerFaultPlan plan)
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal OwnerRow OldValue { get; } = new() { Id = 99 };
    internal OwnerRow Row { get; } = new() { Id = 1, NextId = 2 };
    internal Task<RelationScopeException> Start() => scope.Scheduler.RunAsync(RunAsync, CancellationToken.None);

    private async Task<RelationScopeException> RunAsync(CancellationToken ct)
    {
        using var registration = ct.Register(Cancel);
        Row.Next = OldValue;
        var session = OwnerModel.Session(scope.Binding.Context);
        scope.Attachments.Attach(Row, OwnerModel.Binding, session);
        var work = new PolicyWork(scope, session, OwnerModel.Limits, ct);
        work.Track([Row], OwnerModel.Binding);
        var pending = new OwnerRow { Id = 2 };
        work.Context.Assign(Row, OwnerModel.Edge, pending, () => Row.Next = pending);
        Entered.TrySetResult();
        await Release.Task;
        try { work.Publish(); }
        catch (RelationScopeException error) { return error; }
        finally { plan.Events.Add("drained"); }
        throw new InvalidOperationException("Closed owner published its staged navigation");
    }

    private void Cancel()
    {
        plan.Events.Add("cancel");
        if (plan.ThrowCallback) throw plan.CloseFailure;
    }
}
