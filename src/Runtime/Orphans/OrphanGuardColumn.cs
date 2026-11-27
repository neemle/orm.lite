namespace Neemle.OrmLite;

public sealed record OrphanGuardColumn(string Name, string Type, bool Nullable, bool Key, bool Computed);
