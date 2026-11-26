using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.PolicyCode;

namespace Neemle.OrmLite.Generator;

internal static class PolicyRegistrationEmitter
{
    internal static string? Member(Entity entity)
    {
        var policy = entity.LoadingPolicy;
        if (policy is null || policy.Limits.Length != 6 || !policy.Issues.IsEmpty) return null;
        var values = policy.Limits.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();
        var limits = "new " + Runtime + "GraphLimits(new " + Runtime + "GraphTraversalLimits("
            + string.Join(",", values.Take(3)) + "), new " + Runtime + "GraphWorkLimits("
            + string.Join(",", values.Skip(3)) + "))";
        var policies = policy.Declarations.Select(d => Runtime + "RelationPolicy<" + entity.Full + ">.For(Paths."
            + Identifier(d.Navigation) + ", " + Runtime + "RelationLoadMode." + new[] { "Explicit", "Eager", "Lazy" }[d.
            Mode]);
        return "internal static void RegisterPolicies() { " + Runtime + "RelationPolicies.Register<" + entity.Full
            + ">(" + limits + string.Concat(policies.Select(call => ", " + call + ")")) + "); }";
    }
}
