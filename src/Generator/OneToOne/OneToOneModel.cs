namespace Neemle.OrmLite.Generator;

internal sealed record OneToOneModel(OneToOneIntent Intent, string OwnerFull, string PrincipalFull,
    string ForeignKeyColumn, string PrincipalKeyColumn)
{
    internal string OwnerToken { get; init; } = "";
    internal string PrincipalToken { get; init; } = "";
    internal string KeyType { get; init; } = "";
    internal string PrincipalKeyProperty { get; init; } = "";
    internal bool Nullable { get; init; }
    internal bool NullableValue { get; init; }
    internal string ForeignKeyName { get; init; } = "";
    internal int Action { get; init; }
}
