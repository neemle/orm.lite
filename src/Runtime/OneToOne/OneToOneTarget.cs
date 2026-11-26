namespace Neemle.OrmLite;

public sealed record OneToOneTarget(string Entity, string KeyColumn, string OwnerNavigation, string InverseNavigation);
