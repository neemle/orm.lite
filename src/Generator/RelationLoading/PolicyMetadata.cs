namespace Neemle.OrmLite.Generator;

internal sealed record PolicyMetadata(EquatableArray<PolicyDeclaration> Declarations,
    EquatableArray<int> Limits, EquatableArray<PolicyIssue> Issues);
