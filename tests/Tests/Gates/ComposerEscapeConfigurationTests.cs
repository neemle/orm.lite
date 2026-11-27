using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

// The composer-only gate sees what the generator compiles in each configuration it is built in, and only that.
public sealed class ComposerEscapeConfigurationTests
{
    private static IReadOnlyList<string> Scan(string source) =>
        ComposerEscapeScan.Find([(Path.Combine(RuntimeSources.LibraryRoot(), "src", "Generator", "Planted.cs"), source)]);

    [Theory(DisplayName = "the guard reports an escape in a branch the generator compiles in Release or in Debug")]
    [InlineData("#if NET\nvar p = Cs.Parse(text);\n#endif")]
    [InlineData("#if NET10_0_OR_GREATER\nvar e = Cs.RawExpression(\"x\");\n#endif")]
    [InlineData("#if RELEASE\nvar d = document.Reformat();\n#endif")]
    [InlineData("#if DEBUG\nvar b = Cs.RawBody(\"x\");\n#endif")]
    [InlineData("#if !DEBUG\nvar t = Cs.RawType(\"x\");\n#endif")]
    [InlineData("#if ORMLITE_NEVER_DEFINED\n#else\nvar s = Cs.RawStatement(\"x\");\n#endif")]
    public void Conditional_escapes_are_reported(string planted) =>
        Assert.True(Scan(planted).Count > 0, "the guard did not report: " + planted);

    [Fact(DisplayName = "the guard does not report a branch that no configuration of the generator compiles")]
    public void Branches_no_configuration_compiles_are_not_reported() =>
        Assert.Empty(Scan("#if ORMLITE_NEVER_DEFINED\nvar p = Cs.Parse(text);\n#endif"));

    [Fact(DisplayName = "the symbols are the generator's own: RELEASE in Release, DEBUG in Debug, its target framework in both")]
    public void The_symbols_come_from_the_generator_build()
    {
        var symbols = ComposerEscapeScan.GeneratorSymbols;

        Assert.Contains("RELEASE", symbols["Release"]);
        Assert.DoesNotContain("DEBUG", symbols["Release"]);
        Assert.Contains("DEBUG", symbols["Debug"]);
        Assert.DoesNotContain("RELEASE", symbols["Debug"]);
        Assert.All(symbols.Values, s => Assert.Contains("NET10_0", s));
    }

    [Theory(DisplayName = "a name bound to something other than Composer is not an escape, but a method named nameof hides nothing")]
    [InlineData("static void RawBody(string s) { }\nRawBody(\"x\");", false)]
    [InlineData("var text = \"\";\nvar n = text.Trim().Length;\nvar p = long.Parse(\"1\");", false)]
    [InlineData("static string nameof(object o) => \"\";\nvar s = nameof(Cs.Parse(text));", true)]
    [InlineData("using Neemle.CSharpComposer;\nSystem.Func<string, object> f = Cs.Parse;", true)]
    public void Bound_names_decide(string source, bool reported) =>
        Assert.Equal(reported, Scan(source).Count > 0);
}
