using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public sealed class QueryStateWriteStructuralTests
{
    private static readonly string[] Builders = ["IncludingDeleted", "Take", "Skip"];

    private static readonly (string Target, string[] Writers)[] Slots =
    [
        ("_live", [".ctor", "IncludingDeleted"]),
        ("_select.Limit", ["Take"]),
        ("_select.Offset", ["Skip"]),
    ];

    private static SyntaxNode Root() =>
        CSharpSyntaxTree.ParseText(File.ReadAllText(RuntimeSources.Of("Query.cs"))).GetRoot();

    private static string MemberName(SyntaxNode node) =>
        node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() switch
        {
            MethodDeclarationSyntax m => m.Identifier.ValueText,
            ConstructorDeclarationSyntax => ".ctor",
            PropertyDeclarationSyntax p => p.Identifier.ValueText,
            { } other => other.Kind().ToString(),
            null => "<none>",
        };

    private static List<(string Member, string Target)> Writes() => Root().DescendantNodes()
        .OfType<AssignmentExpressionSyntax>()
        .Select(a => (MemberName(a), a.Left.ToString()))
        .ToList();

    private static List<string> Calls() => Root().DescendantNodes()
        .OfType<InvocationExpressionSyntax>()
        .Select(call => call.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            IdentifierNameSyntax i => i.Identifier.ValueText,
            _ => "",
        })
        .Where(name => Builders.Contains(name))
        .ToList();

    [Fact(DisplayName = "AC7: the soft-delete scope and the Take and Skip of the query are assigned only by the " +
        "builder methods that own them, never by a terminal")]
    public void The_scope_and_the_paging_are_written_only_by_their_builders()
    {
        var violations = Writes()
            .Where(w => Slots.Any(s => s.Target == w.Target && !s.Writers.Contains(w.Member)))
            .Select(w => w.Member + " assigns " + w.Target)
            .ToList();

        Assert.True(violations.Count == 0,
            "Query.cs writes builder state outside its builder method; "
            + "a terminal composes its statement for that call and writes nothing:\n"
            + string.Join("\n", violations));
    }

    [Fact(DisplayName = "AC7: no member of Query.cs calls IncludingDeleted, Take or Skip, so a terminal cannot write " +
        "the scope or the paging through a builder")]
    public void No_member_calls_a_builder_that_writes_the_scope_or_the_paging()
    {
        var calls = Calls();

        Assert.True(calls.Count == 0, "Query.cs calls a builder that writes the query: " + string.Join(", ", calls));
    }

    [Fact(DisplayName = "AC7: the structural census finds every slot and every builder, so it cannot pass by reading " +
        "nothing")]
    public void The_census_finds_every_slot_and_every_builder()
    {
        var writes = Writes();
        var declared = Root().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(m => m.Identifier.ValueText)
            .ToList();

        foreach (var (target, writers) in Slots)
            foreach (var writer in writers)
                Assert.Contains((writer, target), writes);
        foreach (var builder in Builders)
            Assert.Contains(builder, declared);
    }
}
