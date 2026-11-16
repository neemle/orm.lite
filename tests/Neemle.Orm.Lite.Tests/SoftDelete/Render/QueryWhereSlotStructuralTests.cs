using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Render;

public sealed class QueryWhereSlotStructuralTests
{
    private static readonly string[] Writers = ["Where", "WhereExists", "WhereIn"];

    private static readonly string[] Refusals = ["RenderUpdateReturningCore", "RenderDeleteReturningCore", "RenderUpdate", "RenderDelete", "RenderPurge"];

    private const string Renderer = "AppendWhere";

    private const string Reader = "WhereClauses";

    private static (List<string> Allowed, List<string> Violations, int Declarations) Census()
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(RuntimeSources.Of("Query.cs"))).GetRoot();

        var declarations = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Count(v => v.Identifier.ValueText == "_where" && v.Parent?.Parent is FieldDeclarationSyntax);

        var allowed = new List<string>();
        var violations = new List<string>();
        foreach (var id in root.DescendantNodes().OfType<IdentifierNameSyntax>().Where(i => i.Identifier.ValueText == "_where"))
        {
            var member = MemberName(id);
            var line = id.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var access = id.Parent as MemberAccessExpressionSyntax;
            var accessed = access is not null && access.Expression == id ? access.Name.Identifier.ValueText : null;

            if (member == Renderer)
                allowed.Add(Renderer);
            else if (member == Reader && id.Parent is ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax })
                allowed.Add(Reader);
            else if (Writers.Contains(member) && accessed == "Add" && access!.Parent is InvocationExpressionSyntax)
                allowed.Add(member + ":Add");
            else if (Refusals.Contains(member) && accessed == "Count"
                     && access!.Parent is BinaryExpressionSyntax eq
                     && eq.IsKind(SyntaxKind.EqualsExpression)
                     && eq.Left == access
                     && eq.Right is LiteralExpressionSyntax { Token.ValueText: "0" })
                allowed.Add(member + ":Count==0");
            else
                violations.Add("line " + line + " in " + member + ": " + id.Parent);
        }
        return (allowed, violations, declarations);
    }

    private static string MemberName(SyntaxNode node) =>
        node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() switch
        {
            MethodDeclarationSyntax m => m.Identifier.ValueText,
            ConstructorDeclarationSyntax => ".ctor",
            PropertyDeclarationSyntax p => p.Identifier.ValueText,
            FieldDeclarationSyntax f => string.Join(",", f.Declaration.Variables.Select(v => v.Identifier.ValueText)),
            { } other => other.Kind().ToString(),
            null => "<none>",
        };

    [Fact(DisplayName = "Query.cs touches _where only in its declaration, Add in Where/WhereExists/WhereIn, Count == 0 in the five refusals, inside AppendWhere, and as the bare body of the WhereClauses read")]
    public void Query_touches_the_where_slot_only_where_allowed()
    {
        var (_, violations, _) = Census();
        Assert.True(violations.Count == 0,
            "Query.cs touches _where outside its allowed sites; render a WHERE clause through AppendWhere(sb), or read the clauses through WhereClauses:\n"
            + string.Join("\n", violations));
    }

    [Fact(DisplayName = "the structural census finds every allowed _where site, so it cannot pass by reading nothing")]
    public void The_census_finds_every_allowed_site()
    {
        var (allowed, _, declarations) = Census();

        Assert.Equal(1, declarations);
        foreach (var writer in Writers)
            Assert.Contains(writer + ":Add", allowed);
        foreach (var refusal in Refusals)
            Assert.Contains(refusal + ":Count==0", allowed);
        Assert.Contains(Renderer, allowed);
        Assert.Contains(Reader, allowed);
    }
}
