namespace Neemle.OrmLite.Generator.Orphans;

internal sealed record OrphanSafetyFacts(bool PhysicalValuesSafe, EquatableArray<string> Collections);
