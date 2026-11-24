using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using SoftDeclaration = Neemle.OrmLite.Generator.OrmGenerator.SoftDeclaration;
using SoftDeleteIssue = Neemle.OrmLite.Generator.OrmGenerator.SoftDeleteIssue;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

/// <summary>Reads the [SoftDelete] declaration of an entity: its column, its problems and its column lists.</summary>
internal static class SoftDeleteReader
{
    private const string PlatformTenant = "<platform>";

    internal static string? SoftColumnOf(INamedTypeSymbol t) =>
        SoftDeleteDeclaration(t) is { Member: { } member } ? ColumnOf(member) : null;

    internal static (string? Column, SoftDeleteIssue? Issue) ReadSoftDelete(INamedTypeSymbol s, List<Prop> props)
    {
        if (SoftDeleteDeclaration(s) is not (var a, var name, var member)) return (null, null);
        var location = AttributeLocation(a, s);
        string? reason = null;
        if (member is null)
            reason = "is not a property of the entity";
        else if (!IsNullableDateTime(member.Type))
            reason = "is of type '" + member.Type.ToDisplayString() + "', not 'System.DateTime?'";
        else if (!props.Any(p => p.Name == name))
            reason = "is excluded from the column set ([NotAColumn], or not a public read-write instance property)";
        return reason is null ? (name, null) : (null, new SoftDeleteIssue(name, reason, location));
    }

    internal static SoftDeclaration? ReadSoftDeclaration(INamedTypeSymbol s, List<Prop> props)
    {
        if (SoftDeleteDeclaration(s) is not (var a, _, _)) return null;
        var declaredTenant = NamedString(a, "Tenant");
        var tenant = declaredTenant ?? "";
        var platform = tenant == PlatformTenant;
        var tenantProp = platform || declaredTenant is null ? null : props.FirstOrDefault(p => p.Name == tenant);
        var (columns, unknown) = ReadColumnList(a, "Unversioned", props, false);
        var labelName = NamedString(a, "Label");
        var labelProp = labelName is null ? null : props.FirstOrDefault(p => p.Name == labelName);
        var (owners, unknownOwners) = ReadColumnList(a, "Owners", props, true);
        var (pinned, unknownPinned) = ReadColumnList(a, "Pinned", props, true);
        var (status, unknownStatus) = ReadColumnList(a, "StatusColumns", props, true);
        var overlap = status.Where(c => pinned.Contains(c)).ToImmutableArray();
        return new SoftDeclaration(
            tenant, declaredTenant is not null, OrmGenerator.DefaultTenant, platform, tenantProp, columns, unknown,
            AttributeLocation(a, s), labelName, labelProp, owners, unknownOwners, pinned, unknownPinned, status,
            unknownStatus, overlap);
    }

    private static (AttributeData Attribute, string Name, IPropertySymbol? Member)? SoftDeleteDeclaration(
        INamedTypeSymbol t)
    {
        var a = FirstAttr(t, "Neemle.OrmLite.SoftDeleteAttribute");
        if (a is null) return null;
        var named = a.ConstructorArguments.Length > 0 ? a.ConstructorArguments[0].Value as string : null;
        var name = named ?? "DeletedAt";
        return (a, name, PropertiesOf(t).FirstOrDefault(p => p.Name == name));
    }

    private static bool IsNullableDateTime(ITypeSymbol type) =>
        type is INamedTypeSymbol nt
        && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
        && nt.TypeArguments[0].SpecialType == SpecialType.System_DateTime;

    /// <summary>
    /// The columns of the properties that a named array argument lists, and the names that match no property. A
    /// distinct list drops a column that an earlier name already gave.
    /// </summary>
    private static (ImmutableArray<string> Columns, ImmutableArray<string> Unknown) ReadColumnList(
        AttributeData a, string argument, List<Prop> props, bool distinct)
    {
        var columns = new List<string>();
        var unknown = new List<string>();
        var declared = a.NamedArguments.FirstOrDefault(n => n.Key == argument).Value;
        if (declared.Kind == TypedConstantKind.Array)
            foreach (var v in declared.Values)
            {
                var name = v.Value as string ?? "";
                var prop = props.FirstOrDefault(p => p.Name == name);
                if (prop is null) unknown.Add(name);
                else if (!distinct || !columns.Contains(prop.Column)) columns.Add(prop.Column);
            }
        return (columns.ToImmutableArray(), unknown.ToImmutableArray());
    }
}
