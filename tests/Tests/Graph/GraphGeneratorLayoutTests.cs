using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphGeneratorLayoutTests
{
    [Fact(DisplayName = "[301] new generated graph registration and path members stay within 120 columns")]
    public void Generated_graph_members_have_bounded_source_lines()
    {
        var (diagnostics, generated, _) = Run(GraphGeneratorTests.Source);
        Assert.Empty(diagnostics);
        var root = CSharpSyntaxTree.ParseText(generated).GetRoot();
        var registrations = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == "RegisterGraph").ToArray();
        var paths = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText == "Paths").SelectMany(type => type.Members);
        Assert.Equal(3, registrations.Length);
        Assert.NotEmpty(paths);
        foreach (var member in registrations.Cast<Microsoft.CodeAnalysis.SyntaxNode>().Concat(paths))
            foreach (var line in member.ToFullString().Split('\n'))
                Assert.True(line.Length <= 120, "generated graph member exceeds 120 columns: " + line);
    }
}
