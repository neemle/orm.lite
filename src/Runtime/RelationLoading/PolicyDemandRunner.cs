namespace Neemle.OrmLite;

internal static class PolicyDemandRunner
{
    internal static async Task<bool> RunAsync(LoadingScope scope, PolicyDemandWork demand, CancellationToken ct)
    {
        lock (scope.Gate) scope.Attachments.Validate(demand.Entity, demand.Generation);
        using var registry = GraphModel.Enter();
        var limits = PolicyLimits.Intersect(scope.Binding.Options.Limits, demand.Attachment.Session.Limits);
        var model = RelationPolicies.Find(demand.Attachment.Binding.EntityType);
        if (model is not null) limits = PolicyLimits.Intersect(limits, model.Limits);
        var work = new PolicyWork(scope, demand.Attachment.Session, limits, ct);
        PolicyPreflight.Validate(demand.Attachment.Session.Operation(work.Context), GraphModel.Find(demand.Edge.Target));
        work.Track([demand.Entity], demand.Attachment.Binding);
        await new PolicyTraversal(work).DemandAsync(demand);
        work.Publish(demand);
        return true;
    }
}
