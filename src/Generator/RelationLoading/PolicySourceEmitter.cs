using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator;

internal static class PolicySourceEmitter
{
    internal static string Augment(string source, ImmutableArray<Entity> entities,
        HashSet<(string Entity, string Nav)> blocked)
    {
        var accepted = entities;
        entities = entities.Select(e => PolicyDiagnostics.Issues(e, accepted, blocked).Any()
            ? e with { LoadingPolicy = null } : e).ToImmutableArray();
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(type => entities.Any(e => type.Identifier.ValueText == e.Type + "Orm" && GraphEmitter.Supported(e)))
            .ToArray();
        root = root.ReplaceNodes(classes, (type, _) => Members(type,
            entities.Single(e => type.Identifier.ValueText == e.Type + "Orm"), entities));
        return AddInitialization(root, entities).ToFullString();
    }

    private static ClassDeclarationSyntax Members(ClassDeclarationSyntax type, Entity entity,
        ImmutableArray<Entity> entities)
    {
        var existing = type.Members.OfType<MethodDeclarationSyntax>().Select(m => m.Identifier.ValueText).ToHashSet();
        var members = PolicyKeyedEmitter.Members(entity).Concat(PolicyLoaderEmitter.Members(entity, existing, entities)).ToList();
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
