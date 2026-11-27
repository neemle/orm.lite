using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.Orphans;

internal static class OrphanEligibility
{
    internal static bool Supported(Entity entity) => entity.Managed && GraphEmitter.Supported(entity)
        && entity.OrphanSafety is { PhysicalValuesSafe: true } && PhysicalColumns(entity)
        && entity.LoadingPolicy is null && Visible(entity);

    private static bool PhysicalColumns(Entity entity) => entity.Props.Count(p => p.IsKey) == 1
        && OrmGenerator.FirstKey(entity)!.Type != "Json"
        && entity.Props.Select(p => p.Column).Distinct().Count() == entity.Props.Length
        && entity.Props.Select(p => p.Name).Distinct().Count() == entity.Props.Length;

    private static bool Visible(Entity entity) => entity.SoftDeleteIssue is null
        && (entity.Soft is not { } soft || soft.Platform && !soft.TenantDeclared && soft.OwnerColumns.IsEmpty);
}
