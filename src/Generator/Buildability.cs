using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using MemberIssue = Neemle.OrmLite.Generator.OrmGenerator.MemberIssue;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using Unmapped = Neemle.OrmLite.Generator.OrmGenerator.Unmapped;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Whether the generated Map can build an instance of an entity type: the constructor it calls, the required members
/// that constructor does not satisfy, and the text that says why a type cannot be built.
/// </summary>
internal static class Buildability
{
    private const string SetsRequiredMembersName = "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    private const string RequiredReason =
        "it is required, but the generated code cannot populate it from a row, so no row of the entity can be read "
        + "and its Map throws NotSupportedException. Make it a mapped column, drop required, "
        + "or give the parameterless constructor [SetsRequiredMembers]";

    internal static IMethodSymbol? BoundConstructor(INamedTypeSymbol s) =>
        s.InstanceConstructors
            .Where(c => AccessRules.Usable(c.DeclaredAccessibility)
                && c.Parameters.All(p => p.IsOptional || p.IsParams))
            .OrderBy(c => c.Parameters.Length)
            .FirstOrDefault();

    internal static bool SetsRequiredMembers(IMethodSymbol? constructor) =>
        constructor is not null
        && constructor.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == SetsRequiredMembersName);

    internal static string? TypeIssueOf(INamedTypeSymbol s, IMethodSymbol? bound) =>
        s.IsStatic ? "static" : s.IsAbstract ? "abstract" : bound is null ? "constructor" : null;

    internal static string? AddRequiredIssues(
        INamedTypeSymbol s, List<Prop> props, List<Unmapped> unmapped, bool setsRequired, List<MemberIssue> issues)
    {
        if (s.IsStatic || setsRequired) return null;
        var populated = new HashSet<string>(props.Where(p => p.InInit).Select(p => p.Name));
        var covered = new HashSet<string>(issues.Select(i => i.Member).Concat(unmapped.Select(u => u.Property)));
        string? first = null;
        foreach (var member in SymbolFacts.ChainOf(s).SelectMany(t => t.GetMembers()))
        {
            if (!IsRequiredMember(member) || populated.Contains(member.Name)) continue;
            first ??= member.Name;
            var location = SymbolFacts.LocationOf(member);
            if (covered.Add(member.Name)) issues.Add(new MemberIssue(member.Name, RequiredReason, location));
        }
        return first;
    }

    internal static bool IsUnbuildable(Entity e) =>
        !e.IsStatic && (e.TypeIssue is not null || e.RequiredMissing is not null);

    internal static string TypeText(string issue) => issue switch
    {
        "static" => "it is a static class, which cannot be a return type, a parameter or a type argument, "
            + "so static persistence types are not supported and it is not generated. Remove static",
        "abstract" => "it is abstract, so no row can be turned into an instance and its Map throws "
            + "NotSupportedException. Make the class concrete",
        _ => "it has no accessible constructor that can be called without arguments (a positional record or a class "
            + "with a primary constructor is the usual cause), so no row can be turned into an instance, "
            + "constructor based materialisation is not supported and its Map throws NotSupportedException. "
            + "Declare a parameterless constructor that is public, internal or protected internal",
    };

    internal static string ThrowText(Entity e)
    {
        var reason = e.TypeIssue switch
        {
            "abstract" => "it is abstract",
            "constructor" => "it has no accessible constructor that can be called without arguments",
            _ => "the required member '" + e.RequiredMissing + "' cannot be populated from a row",
        };
        return e.Type + " cannot be built from a row because " + reason + ", so reading it is not supported";
    }

    private static bool IsRequiredMember(ISymbol member) => member switch
    {
        IPropertySymbol { IsStatic: false, IsIndexer: false, IsRequired: true } => true,
        IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false, IsRequired: true } => true,
        _ => false,
    };
}
