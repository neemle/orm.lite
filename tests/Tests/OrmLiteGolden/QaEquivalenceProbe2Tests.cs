using Xunit;
using Xunit.Abstractions;

namespace Neemle.OrmLite.Tests.OrmLiteGolden;

// QA ORML-156 probe round 2: §803 LIGHT L2/L3 witnesses. Each asserts what AC3 requires.
public sealed class QaEquivalenceProbe2Tests(ITestOutputHelper output)
{
    private const string Golden = "tests/Tests/OrmLiteGolden/Expected/OrmLite.Entities.0000.g.cs.golden";

    private string? Diff(string name, string a, string b)
    {
        var d = GeneratedCodeEquivalence.Difference(a, b);
        output.WriteLine($"PROBE2 [{name}] {(d is null ? "EQUIVALENT" : "DIFFERENT: " + d)}");
        return d;
    }

    [Fact]
    public void L2_real_golden_nullable_directive_moved_below_the_first_class_must_fail()
    {
        var pinned = File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), Golden.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n");
        Assert.Contains("#nullable enable\n", pinned);
        var without = pinned.Replace("#nullable enable\n", "", StringComparison.Ordinal);
        var last = without.LastIndexOf("public static class ", StringComparison.Ordinal);
        Assert.True(last > 0);
        var moved = without[..last] + "#nullable enable\n" + without[last..];
        Assert.NotNull(Diff("L2 real golden #nullable moved", pinned, moved));
    }

    [Fact]
    public void L2_synthetic_nullable_scope_moved_must_fail()
    {
        const string a = "#nullable disable\nclass A { public string F; }\n#nullable enable\nclass B { public string F; }\n";
        const string b = "#nullable disable\n#nullable enable\nclass A { public string F; }\nclass B { public string F; }\n";
        Assert.NotNull(Diff("L2 synthetic #nullable scope", a, b));
    }

    [Fact]
    public void L2_pragma_restore_moved_must_fail()
    {
        const string a = "#pragma warning disable CS0618\nclass A { }\n#pragma warning restore CS0618\nclass B { }\n";
        const string b = "#pragma warning disable CS0618\nclass A { }\nclass B { }\n#pragma warning restore CS0618\n";
        Assert.NotNull(Diff("L2 pragma restore moved", a, b));
    }

    [Fact]
    public void L2_doc_comment_moved_between_declarations_must_fail()
    {
        const string a = "/// <summary>A</summary>\nclass A { }\nclass B { }\n";
        const string b = "class A { }\n/// <summary>A</summary>\nclass B { }\n";
        Assert.NotNull(Diff("L2 doc comment moved", a, b));
    }

    [Fact]
    public void L3_disabled_branch_string_spacing_must_fail()
    {
        const string a = "class C {\n#if FEATURE\nconst string S = \"a  b\";\n#endif\n}\n";
        const string b = "class C {\n#if FEATURE\nconst string S = \"a b\";\n#endif\n}\n";
        Assert.NotNull(Diff("L3 disabled string spacing", a, b));
    }

    [Fact]
    public void L3_directive_expression_spacing_is_formatting()
    {
        const string a = "class C {\n#if (true&&true)\nint X;\n#endif\n}\n";
        const string b = "class C {\n#if (true && true)\nint X;\n#endif\n}\n";
        Assert.Null(Diff("L3 directive spacing", a, b));
    }

    [Fact]
    public void Var_versus_verbatim_var()
    {
        const string a = "class C { void M() { var x = 1; } }";
        const string b = "class C { void M() { @var x = 1; } }";
        Assert.NotNull(Diff("var vs @var", a, b));
    }
}
