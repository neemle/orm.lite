using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// How the generated code names an entity type: whether it can name it at all, the access it gets, and the class name
/// token of two entities that share a simple name in different namespaces.
/// </summary>
internal static class EntityNaming
{
    private const string GenericName = "generic";

    private const string FileName = "file";

    internal static string? NameIssueOf(INamedTypeSymbol s)
    {
        var chain = EnclosingTypes(s).ToList();
        if (chain.Any(t => t.IsGenericType)) return GenericName;
        if (chain.Any(t => t.IsFileLocal)) return FileName;
        return chain.All(t => AccessRules.Usable(t.DeclaredAccessibility)) ? null : "inaccessible";
    }

    internal static string AccessOf(INamedTypeSymbol s) =>
        EnclosingTypes(s).All(t => t.DeclaredAccessibility == Accessibility.Public) ? "public" : "internal";

    internal static string NameText(string issue) => issue switch
    {
        GenericName => "it is a generic type, or nested in one, and the generated code cannot supply type "
            + "arguments, so a generic entity is not supported and it is not generated. Declare a closed class",
        FileName => "it is a file-local type, which the generated file cannot name, so it is not generated. "
            + "Remove the file modifier",
        _ => "it, or a type it is nested in, is private, protected or private protected, which the generated "
            + "code cannot name, so it is not generated. Make it public or internal",
    };

    /// <summary>
    /// Gives each entity whose simple name is shared its own class name token, built from the namespace path that
    /// tells the entities apart; a dropped entity gets its whole path so no other token can collide with it.
    /// </summary>
    internal static ImmutableArray<Entity> Disambiguated(ImmutableArray<Entity> all)
    {
        var tokens = TokensOf(all);
        return tokens.Count == 0 ? all : all.Select(e => Renamed(e, tokens)).ToImmutableArray();
    }

    private static Dictionary<string, string> TokensOf(ImmutableArray<Entity> all)
    {
        var tokens = new Dictionary<string, string>();
        var groups = all.Where(e => !OrmGenerator.Dropped(e)).GroupBy(e => e.Type).ToList();
        var used = new HashSet<string>(groups.Where(g => g.Count() == 1).Select(g => g.Key));
        foreach (var group in groups.Where(g => g.Count() > 1))
        {
            var paths = group.Select(PathOf).ToList();
            var skip = Math.Min(SharedSegments(paths), paths.Min(p => p.Length) - 1);
            foreach (var e in group)
                tokens[e.Full] = Reserve(used, string.Join("_", PathOf(e).Skip(skip)));
        }
        foreach (var e in all.Where(OrmGenerator.Dropped))
            tokens[e.Full] = Reserve(used, string.Join("_", PathOf(e)));
        return tokens;
    }

    private static string[] PathOf(Entity e) => e.Full.Substring(GlobalPrefix.Length).Split('.');

    private static int SharedSegments(List<string[]> paths)
    {
        var count = 0;
        while (paths.All(p => p.Length > count && p[count] == paths[0][count])) count++;
        return count;
    }

    private static string Reserve(HashSet<string> used, string token)
    {
        while (!used.Add(token)) token += "_";
        return token;
    }

    private static string Mapped(Dictionary<string, string> tokens, string full, string fallback) =>
        tokens.TryGetValue(full, out var token) ? token : fallback;

    private static Entity Renamed(Entity e, Dictionary<string, string> tokens) => e with
    {
        Type = Mapped(tokens, e.Full, e.Type),
        OneToOnePairs = e.OneToOnePairs.Select(p => p with
        {
            OwnerToken = Mapped(tokens, p.OwnerFull, p.OwnerToken),
            PrincipalToken = Mapped(tokens, p.PrincipalFull, p.PrincipalToken),
        }).ToImmutableArray(),
        Relations = e.Relations
            .Select(r => r with { TargetType = Mapped(tokens, r.TargetFull, r.TargetType) })
            .ToImmutableArray(),
        Collections = e.Collections
            .Select(c => c with { ChildType = Mapped(tokens, c.ChildFull, c.ChildType) })
            .ToImmutableArray(),
        ManyToMany = e.ManyToMany
            .Select(m => m with { OtherType = Mapped(tokens, m.OtherFull, m.OtherType) })
            .ToImmutableArray(),
    };
}
