using System.Reflection;
using Neemle.Orm.Lite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Render;

public sealed class QueryPrivateMemberReflectionGuardTests
{
    private static readonly string[] Lookups = ["GetField", "GetMethod", "GetProperty", "GetMember"];

    private static readonly (string Source, string Type, Type Runtime)[] Subjects =
    [
        ("Query.cs", "Query", typeof(Query<>)),
        ("StatementExecutor.cs", "StatementExecutor", typeof(Query<>).Assembly.GetType("Neemle.Orm.Lite.StatementExecutor", throwOnError: true)!),
    ];

    private static (HashSet<string> NonPublic, HashSet<string> Public) Declared(string source, string type)
    {
        var cls = CSharpSyntaxTree.ParseText(File.ReadAllText(RuntimeSources.Of(source))).GetRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == type);

        var nonPublic = new HashSet<string>(StringComparer.Ordinal);
        var @public = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in cls.Members)
        {
            var names = member switch
            {
                FieldDeclarationSyntax f => f.Declaration.Variables.Select(v => v.Identifier.ValueText),
                MethodDeclarationSyntax m => [m.Identifier.ValueText],
                PropertyDeclarationSyntax p => [p.Identifier.ValueText],
                _ => [],
            };
            var target = member.Modifiers.Any(SyntaxKind.PublicKeyword) ? @public : nonPublic;
            foreach (var name in names)
                target.Add(name);
        }
        return (nonPublic, @public);
    }

    private static HashSet<string> Collected()
    {
        var collected = new HashSet<string>(StringComparer.Ordinal);
        var published = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (source, type, _) in Subjects)
        {
            var (nonPublic, @public) = Declared(source, type);
            collected.UnionWith(nonPublic);
            published.UnionWith(@public);
        }
        collected.ExceptWith(published);
        return collected;
    }

    private static HashSet<string> Reflected(Type type)
    {
        const BindingFlags Flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        return type.GetFields(Flags).Select(f => f.Name)
            .Concat(type.GetMethods(Flags).Where(m => !m.IsSpecialName).Select(m => m.Name))
            .Concat(type.GetProperties(Flags).Select(p => p.Name))
            .Where(n => !n.Contains('<'))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static List<string> Hits(string source, string label, HashSet<string> names)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var hits = new List<string>();
        foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var callee = call.Expression switch
            {
                MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
                IdentifierNameSyntax i => i.Identifier.ValueText,
                _ => null,
            };
            if (callee is null || !Lookups.Contains(callee))
                continue;
            if (call.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax literal
                || !literal.IsKind(SyntaxKind.StringLiteralExpression)
                || !names.Contains(literal.Token.ValueText))
                continue;
            hits.Add(label + ":" + (call.GetLocation().GetLineSpan().StartLinePosition.Line + 1) + " " + callee + "(\"" + literal.Token.ValueText + "\")");
        }
        return hits;
    }

    private static string Self() => Path.Combine(RuntimeSources.LibraryRoot(), "tests", "Neemle.Orm.Lite.Tests", "SoftDelete", "Render", "QueryPrivateMemberReflectionGuardTests.cs");

    private static IEnumerable<string> TestSources()
    {
        var root = RuntimeSources.LibraryRoot();
        return new[] { Path.Combine(root, "tests") }
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj"));
    }

    [Fact(DisplayName = "the member census reads every non-public field, method and property name Query<T> and StatementExecutor declare, so the guard cannot pass by collecting nothing")]
    public void The_census_reads_every_non_public_member_name_the_runtime_declares()
    {
        foreach (var (source, type, runtime) in Subjects)
        {
            var (nonPublic, _) = Declared(source, type);
            Assert.NotEmpty(nonPublic);
            Assert.Equal(Reflected(runtime).Order(), nonPublic.Order());
        }
        Assert.NotEmpty(Collected());
    }

    [Fact(DisplayName = "the scanner flags a string-literal GetField, GetMethod, GetProperty or GetMember of a collected name and nothing else")]
    public void The_scanner_flags_planted_lookups_of_collected_names()
    {
        var names = Collected();
        var name = names.Order().First();

        foreach (var lookup in new[] { "GetField", "GetMethod", "GetProperty", "GetMember" })
        {
            var planted = "class P { object M(System.Type t) => t." + lookup + "(\"" + name + "\", System.Reflection.BindingFlags.NonPublic)!; }";
            Assert.True(Hits(planted, "planted", names).Count == 1, lookup + " of the collected name " + name + " was not flagged exactly once");
        }

        Assert.Single(Hits("class P { object M(System.Type t) => t.GetMethod(\"" + name + "\") ?? throw new MissingMethodException(); }", "planted", names));
        Assert.Empty(Hits("class P { object M(System.Type t) => t.GetMethod(\"NoSuchMemberAnywhere\"); }", "planted", names));
        Assert.Empty(Hits("class P { object M(System.Type t) => t.GetMethod(nameof(P.M)); }", "planted", names));
        Assert.Empty(Hits("class P { object M(System.Type t) => t.GetMethods(\"" + name + "\"); }", "planted", names));

        foreach (var publicName in Subjects.SelectMany(s => Declared(s.Source, s.Type).Public).Distinct())
            Assert.Empty(Hits("class P { object M(System.Type t) => t.GetMethod(\"" + publicName + "\"); }", "planted", names));
    }

    [Fact(DisplayName = "no test source in the library reflects by string literal into a non-public member of Query<T> or StatementExecutor")]
    public void No_test_source_reflects_into_a_private_member_of_the_query_builder()
    {
        var names = Collected();
        var root = RuntimeSources.LibraryRoot();
        var files = TestSources().ToList();
        var self = Self();
        Assert.Contains(self, files);

        var hits = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            if (Lookups.Any(text.Contains))
                hits.AddRange(Hits(text, Path.GetRelativePath(root, file), names));
        }

        Assert.True(hits.Count == 0,
            "A test reflects into a non-public member of Query<T> or StatementExecutor; read it through the internal surface (WhereClauses, BoundParameters, Bind) instead:\n"
            + string.Join("\n", hits));
    }
}
