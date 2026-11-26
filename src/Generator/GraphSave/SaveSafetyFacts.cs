namespace Neemle.OrmLite.Generator.GraphSave;

internal sealed record SaveSafetyFacts(bool PhysicalValuesSafe,
    EquatableArray<string> SafeReferences, EquatableArray<string> SafeLists);
