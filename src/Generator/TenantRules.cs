using Prop = Neemle.OrmLite.OrmGenerator.Prop;

namespace Neemle.OrmLite;

/// <summary>What makes a property unusable as the tenant of a [SoftDelete] entity (ORMLITE007).</summary>
internal static class TenantRules
{
    /// <summary>The reason the property cannot be the tenant, or null when it can.</summary>
    internal static string? ReasonOf(Prop? tenant)
    {
        if (tenant is null) return "is not a mapped property of the entity";
        if (tenant.Read != "Guid") return "is of type '" + tenant.Cs + "', not System.Guid";
        return tenant.Nullable ? "is of type 'System.Guid?', which is nullable; the tenant must be System.Guid" : null;
    }
}
