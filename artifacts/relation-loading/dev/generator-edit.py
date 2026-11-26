from pathlib import Path
p=Path('src/Generator/RelationLoading');p.mkdir(exist_ok=True)
(p/'.editorconfig').write_text('[*.cs]\ndotnet_style_namespace_match_folder = false\n')
f={
'PolicyDeclaration.cs':'''using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

internal sealed record PolicyDeclaration(string Navigation, int Mode, Location Location);
''',
'PolicyIssue.cs':'''using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

internal sealed record PolicyIssue(string Navigation, string Reason, Location Location);
''',
'PolicyMetadata.cs':'''namespace Neemle.OrmLite.Generator;

internal sealed record PolicyMetadata(EquatableArray<PolicyDeclaration> Declarations,
    EquatableArray<int> Limits, EquatableArray<PolicyIssue> Issues);
''',
'RelationPolicyReader.cs':'''using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

internal static class RelationPolicyReader
{
    private const string ModeAttribute = "Neemle.OrmLite.RelationLoadAttribute";
    private const string LimitsAttribute = "Neemle.OrmLite.RelationLoadLimitsAttribute";
    private static readonly string[] Dimensions =
        ["MaxDepth", "MaxPaths", "MaxNodes", "MaxRoots", "MaxRelatedRows", "MaxStatements"];

    internal static Entity Read(INamedTypeSymbol type, Entity entity)
    {
        var declarations = new List<PolicyDeclaration>();
        var issues = new List<PolicyIssue>();
        foreach (var property in PropertiesOf(type)) ReadProperty(property, declarations, issues);
        var limits = ReadLimits(type, declarations, issues);
        if (declarations.Count == 0 && type.GetAttributes().All(a => !Is(a, LimitsAttribute))) return entity;
        return entity with { LoadingPolicy = new PolicyMetadata(declarations.ToImmutableArray(), limits,
            issues.ToImmutableArray()) };
    }

    private static void ReadProperty(IPropertySymbol property, List<PolicyDeclaration> declarations,
        List<PolicyIssue> issues)
    {
        var attributes = property.GetAttributes().Where(a => Is(a, ModeAttribute)).ToArray();
        if (attributes.Length == 0) return;
        var mode = attributes[0].ConstructorArguments.FirstOrDefault().Value;
        if (attributes.Length != 1 || mode is not int value || value < 0 || value > 2)
            issues.Add(new PolicyIssue(property.Name, "invalid or conflicting relation load mode", LocationOf(property)));
        else declarations.Add(new PolicyDeclaration(property.Name, value, LocationOf(property)));
    }

    private static ImmutableArray<int> ReadLimits(INamedTypeSymbol type, List<PolicyDeclaration> declarations,
        List<PolicyIssue> issues)
    {
        var attributes = type.GetAttributes().Where(a => Is(a, LimitsAttribute)).ToArray();
        if (attributes.Length == 0 && declarations.All(p => p.Mode == 0)) return [];
        var values = attributes.Length == 1 ? attributes[0].NamedArguments.ToDictionary(p => p.Key, p => p.Value.Value)
            : new Dictionary<string, object?>();
        var limits = Dimensions.Select(name => values.TryGetValue(name, out var value) && value is int count
            ? count : 0).ToImmutableArray();
        if (attributes.Length != 1 || limits.Any(value => value <= 0))
            issues.Add(new PolicyIssue(type.Name, "all six positive named load limits are required", LocationOf(type)));
        return limits;
    }

    private static bool Is(AttributeData attribute, string name) => attribute.AttributeClass?.ToDisplayString() == name;
}
''',
'PolicyDiagnostics.cs':'''using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator;

internal static class PolicyDiagnostics
{
    private static readonly DiagnosticDescriptor Invalid = new("ORMLITE035", "Invalid relation loading policy",
        "Relation policy '{0}.{1}' is invalid: {2}.", "OrmLite", DiagnosticSeverity.Error, true);

    internal static void Report(SourceProductionContext context, ImmutableArray<Entity> entities,
        HashSet<(string Entity, string Nav)> blocked)
    {
        foreach (var entity in entities)
            foreach (var issue in Issues(entity, entities, blocked))
                context.ReportDiagnostic(Diagnostic.Create(Invalid, issue.Location, entity.Full,
                    issue.Navigation, issue.Reason));
    }

    internal static IEnumerable<PolicyIssue> Issues(Entity entity, ImmutableArray<Entity> entities,
        HashSet<(string Entity, string Nav)> blocked)
    {
        if (entity.LoadingPolicy is not { } policy) yield break;
        foreach (var issue in policy.Issues) yield return issue;
        foreach (var declaration in policy.Declarations)
        {
            var target = Target(entity, declaration.Navigation);
            if (!GraphEmitter.Supported(entity) || target is null
                || !entities.Any(e => e.Full == target && GraphEmitter.Supported(e))
                || blocked.Contains((entity.Full, declaration.Navigation)))
                yield return new PolicyIssue(declaration.Navigation, "navigation has no supported graph binding",
                    declaration.Location);
        }
    }

    private static string? Target(Entity entity, string navigation) =>
        entity.Relations.FirstOrDefault(r => r.Nav == navigation)?.TargetFull
        ?? entity.Collections.FirstOrDefault(c => c.Nav == navigation)?.ChildFull
        ?? entity.ManyToMany.FirstOrDefault(m => m.Nav == navigation)?.OtherFull;
}
''',
'PolicyCode.cs':'''using Microsoft.CodeAnalysis.CSharp;

namespace Neemle.OrmLite.Generator;

internal static class PolicyCode
{
    internal const string Runtime = "global::Neemle.OrmLite.";
    internal const string Task = "global::System.Threading.Tasks.Task";
    internal const string Token = "global::System.Threading.CancellationToken";
    internal const string Options = Runtime + "RelationReadOptions options, " + Token + " ct = default";
    internal static string Identifier(string value) => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None
        || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None ? "@" + value : value;
    internal static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
}
''',
'PolicyKeyedEmitter.cs':'''using System.Collections.Generic;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.PolicyCode;

namespace Neemle.OrmLite.Generator;

internal static class PolicyKeyedEmitter
{
    internal static IEnumerable<string> Members(Entity entity)
    {
        var key = OrmGenerator.FirstKey(entity)!;
        foreach (var bound in new[] { false, true })
        {
            var parameter = bound ? "global::System.Data.Common.DbConnection conn, " : "";
            var on = bound ? ".On(conn, options.Scope is null ? null : options.ScopeTransaction())" : "";
            // Transaction binding is supplied and validated by the scope; connection-only query binding is exact.
            on = bound ? ".On(conn)" : "";
            yield return "public static " + Task + "<" + entity.Full + "?> GetAsync(" + Runtime
                + "OrmContext db, " + parameter + key.Cs + " key, " + Options + ") => "
                + Runtime + "RelationQueryExtensions.FirstOrDefaultAsync(Query(db)" + on + ".Where(e => e."
                + Identifier(key.Name) + " == key), options, ct);";
        }
    }
}
''',
'PolicyLoaderEmitter.cs':'''using System.Collections.Generic;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.PolicyCode;

namespace Neemle.OrmLite.Generator;

internal static class PolicyLoaderEmitter
{
    internal static IEnumerable<string> Members(Entity entity, HashSet<string> existing)
    {
        foreach (var relation in entity.Relations.Where(r => existing.Contains("Load" + r.Nav + "Async")))
        {
            var key = "e." + Identifier(relation.FkProp);
            var value = relation.FkNullable && relation.FkValue ? key + ".Value" : key;
            var call = relation.TargetType + "Orm.GetAsync(db, conn, " + value + ", options, ct)";
            var assign = "e." + Identifier(relation.Nav) + " = await " + call + ";";
            if (relation.FkNullable) assign = "if (" + key + " is null) e." + Identifier(relation.Nav)
                + " = null; else " + assign;
            yield return Header(entity, relation.Nav, true) + " { ct.ThrowIfCancellationRequested(); " + assign + " }";
        }
        foreach (var collection in entity.Collections.Where(c => existing.Contains("Load" + c.Nav + "Async")))
            yield return Header(entity, collection.Nav, false) + " { e." + Identifier(collection.Nav) + " = await "
                + Runtime + "RelationQueryExtensions.ToListAsync(" + collection.ChildType + "Orm.Query(db).Where(c => c."
                + Identifier(collection.InverseFk) + " == e." + Identifier(collection.ParentKey) + "), options, ct); }";
        foreach (var link in entity.ManyToMany.Where(m => existing.Contains("Load" + m.Nav + "Async")))
            yield return Linked(entity, link);
    }

    private static string Header(Entity entity, string navigation, bool connection) =>
        "public static async " + Task + " Load" + Identifier(navigation) + "Async(" + Runtime + "OrmContext db, "
        + (connection ? "global::System.Data.Common.DbConnection conn, " : "") + entity.Full + " e, " + Options + ")";

    private static string Linked(Entity entity, OrmGenerator.ManyToMany link)
    {
        var key = OrmGenerator.FirstKey(entity)!;
        var read = "new " + Runtime + "LinkedRead<" + link.OtherFull + ">(Descriptor, " + Literal(link.Nav)
            + ", e." + Identifier(key.Name) + ", " + link.OtherType + "Orm.Descriptor, " + link.OtherType + "Orm.Map)";
        return Header(entity, link.Nav, true) + " { e." + Identifier(link.Nav) + " = await " + Runtime
            + "OrmExec.LoadLinkedAsync(db, conn, " + read + ", options, ct); }";
    }
}
''',
'PolicyRegistrationEmitter.cs':'''using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.PolicyCode;

namespace Neemle.OrmLite.Generator;

internal static class PolicyRegistrationEmitter
{
    internal static string? Member(Entity entity)
    {
        var policy = entity.LoadingPolicy;
        if (policy is null || policy.Limits.Length != 6 || !policy.Issues.IsEmpty) return null;
        var values = policy.Limits.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();
        var limits = "new " + Runtime + "GraphLimits(new " + Runtime + "GraphTraversalLimits("
            + string.Join(",", values.Take(3)) + "), new " + Runtime + "GraphWorkLimits("
            + string.Join(",", values.Skip(3)) + "))";
        var policies = policy.Declarations.Select(d => Runtime + "RelationPolicy<" + entity.Full + ">.For(Paths."
            + Identifier(d.Navigation) + ", " + Runtime + "RelationLoadMode." + new[] { "Explicit", "Eager", "Lazy" }[d.Mode]);
        return "internal static void RegisterPolicies() { " + Runtime + "RelationPolicies.Register<" + entity.Full
            + ">(" + limits + string.Concat(policies.Select(call => ", " + call + ")")) + "); }";
    }
}
''',
'PolicySourceEmitter.cs':'''using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator;

internal static class PolicySourceEmitter
{
    internal static string Augment(string source, ImmutableArray<Entity> entities)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(type => entities.Any(e => type.Identifier.ValueText == e.Type + "Orm" && GraphEmitter.Supported(e)))
            .ToArray();
        root = root.ReplaceNodes(classes, (type, _) => Members(type,
            entities.Single(e => type.Identifier.ValueText == e.Type + "Orm")));
        return AddInitialization(root, entities).ToFullString();
    }

    private static ClassDeclarationSyntax Members(ClassDeclarationSyntax type, Entity entity)
    {
        var existing = type.Members.OfType<MethodDeclarationSyntax>().Select(m => m.Identifier.ValueText).ToHashSet();
        var members = PolicyKeyedEmitter.Members(entity).Concat(PolicyLoaderEmitter.Members(entity, existing)).ToList();
        if (PolicyRegistrationEmitter.Member(entity) is { } registration) members.Add(registration);
        return type.AddMembers(members.Select(Parse).ToArray());
    }

    private static CompilationUnitSyntax AddInitialization(CompilationUnitSyntax root, ImmutableArray<Entity> entities)
    {
        var calls = entities.Where(e => GraphEmitter.Supported(e) && PolicyRegistrationEmitter.Member(e) is not null)
            .Select(e => e.Type + "Orm.RegisterPolicies();").ToArray();
        if (calls.Length == 0) return root;
        var initializer = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "OrmMetaInit");
        var init = initializer.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Init");
        var modified = initializer.ReplaceNode(init, init.WithBody(init.Body!.AddStatements(
            SyntaxFactory.ParseStatement("RegisterPolicies();"))));
        modified = modified.AddMembers(Parse("private static void RegisterPolicies() { " + string.Join(" ", calls) + " }"));
        return root.ReplaceNode(initializer, modified);
    }

    private static MemberDeclarationSyntax Parse(string source) => SyntaxFactory.ParseMemberDeclaration(source)!
        .NormalizeWhitespace().WithLeadingTrivia(SyntaxFactory.LineFeed).WithTrailingTrivia(SyntaxFactory.LineFeed);
}
'''
}
for n,s in f.items():(p/n).write_text(s)
p=Path('src/Generator/OrmGenerator.cs');s=p.read_text().replace('''        public EquatableArray<CalculatedProp> Calculated''','''        public PolicyMetadata? LoadingPolicy { get; init; }

        public EquatableArray<CalculatedProp> Calculated''');p.write_text(s)
p=Path('src/Generator/EntityReader.cs');s=p.read_text().replace('''        return Assemble(scan, TableOf(s, view), bound);''','''        return RelationPolicyReader.Read(s, Assemble(scan, TableOf(s, view), bound));''');p.write_text(s)
p=Path('src/Generator/EntityEmitter.cs');s=p.read_text().replace('''        var named = EntityNaming.Disambiguated(all);''','''        var named = EntityNaming.Disambiguated(all);
        PolicyDiagnostics.Report(spc, named, blocked);''').replace('''        return file.Render(RenderOptions);
    }

    private static HashSet''','''        return PolicySourceEmitter.Augment(file.Render(RenderOptions), entities);
    }

    private static HashSet''');p.write_text(s)
