using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

public sealed class SdetDiagnosticPrivacyTests
{
    [Theory(DisplayName = "[SEC][UC-003] malformed generated FK names never disclose their payload or row defaults")]
    [InlineData("\u0000")]
    [InlineData("\r")]
    [InlineData("\t")]
    public void Invalid_name_diagnostics_expose_category_without_sensitive_input(string control)
    {
        const string privateName = "credential-payload-DO-NOT-LOG";
        const string rowDefault = "row-value-DO-NOT-LOG";
        var literal = SymbolDisplay.FormatLiteral(control + privateName, true);
        var source = ForeignKeyGeneration.Source(
            "[ForeignKey(" + literal + ", ReferentialAction.Cascade)]");
        source = source.Replace("public Guid? ParentId", "public string Secret { get; set; } = \""
            + rowDefault + "\"; public Guid? ParentId");
        var (diagnostics, generated, output) = Run(source);
        var issue = Assert.Single(diagnostics, item => item.Id == "ORMLITE035");
        Assert.Contains("InvalidName", issue.GetMessage());
        Assert.DoesNotContain(privateName, issue.GetMessage());
        Assert.DoesNotContain(rowDefault, issue.GetMessage());
        Assert.True(issue.Location.IsInSource);
        Assert.DoesNotContain("class " + output.AssemblyName + "Schema", generated);
    }
}
