using System.Text.RegularExpressions;
using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

public sealed class GeneratorComposerOnlyTests
{
    private static readonly Regex RawEscape = new(@"\bRaw[A-Z][A-Za-z]*\s*\(|\bCs\s*\.\s*Parse(Tree)?\s*\(|\.\s*Reformat\s*\(", RegexOptions.CultureInvariant);

    private static string GeneratorDirectory() => Path.Combine(RuntimeSources.LibraryRoot(), "src", "Generator");

    private static IEnumerable<string> Sources() => LiteralScan.SourceFiles(GeneratorDirectory());

    [Fact(DisplayName = "no source of the generator calls a Composer Raw escape, parses text into a document or reformats one")]
    public void No_generator_source_uses_a_raw_escape()
    {
        var hits = Sources()
            .SelectMany(f => RawEscape.Matches(File.ReadAllText(f)).Select(m => Path.GetFileName(f) + ": " + m.Value))
            .ToList();

        Assert.True(hits.Count == 0, string.Join("\n", hits));
    }

    [Theory(DisplayName = "the Raw-escape guard reports each planted escape")]
    [InlineData("var b = Cs.RawBody(\"x\");")]
    [InlineData("var e = Cs.RawExpression(\"x\");")]
    [InlineData("var s = Cs.RawStatement(\"x\");")]
    [InlineData("var t = Cs.RawType(\"x\");")]
    [InlineData("builder.RawBody(\"x\");")]
    [InlineData("var p = Cs.Parse(text);")]
    [InlineData("var d = document.Reformat();")]
    public void The_guard_catches_planted_escapes(string planted) => Assert.Matches(RawEscape, planted);

    [Fact(DisplayName = "the generator holds no template engine, no template file and no embedded resource, and the runtime no [Sql] or [Repository] attribute")]
    public void No_template_remains()
    {
        var directory = GeneratorDirectory();

        Assert.False(File.Exists(Path.Combine(directory, "Template.cs")));
        Assert.False(Directory.Exists(Path.Combine(directory, "Templates")));
        Assert.Empty(Directory.GetFiles(directory, "*.cs-template", SearchOption.AllDirectories));
        var project = File.ReadAllText(Path.Combine(directory, "Generator.csproj"));
        Assert.DoesNotContain("EmbeddedResource", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Templating", string.Join("\n", Sources().Select(File.ReadAllText)), StringComparison.Ordinal);
        var runtime = Path.Combine(RuntimeSources.LibraryRoot(), "src", "Runtime");
        Assert.False(File.Exists(Path.Combine(runtime, "SqlAttribute.cs")));
        Assert.False(File.Exists(Path.Combine(runtime, "RepositoryAttribute.cs")));
    }

    [Fact(DisplayName = "the generator project targets net10.0 and takes Neemle.CSharpComposer at the pinned version")]
    public void The_generator_project_pins_composer()
    {
        var project = File.ReadAllText(Path.Combine(GeneratorDirectory(), "Generator.csproj"));

        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Neemle.CSharpComposer\" Version=\"$(OrmLiteComposerVersion)\"",
            project, StringComparison.Ordinal);
        var props = File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), "Directory.Build.props"));
        Assert.Contains("<OrmLiteComposerVersion>0.6.0</OrmLiteComposerVersion>", props, StringComparison.Ordinal);
    }
}
