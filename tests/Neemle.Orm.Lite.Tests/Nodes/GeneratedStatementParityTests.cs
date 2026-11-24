using System.Text.RegularExpressions;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Tests.Corpus;
using Neemle.Orm.Lite.Tests.Corpus.Cases;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

public sealed partial class GeneratedStatementParityTests
{
    private const string Frozen = "tests/Neemle.Orm.Lite.Tests/Nodes/LegacyGenerated/";

    [GeneratedRegex(@"public static class (?<type>\w+)Orm\r?\n\{(?<body>.*?)\r?\n\}\r?\n", RegexOptions.Singleline)]
    private static partial Regex ClassBlock();

    [GeneratedRegex(@"public static async Task(?:<[^\r\n]*>)? (?<name>\w+)\(DbConnection conn[^\r\n]*\)\r?\n    \{.*?(?:cmd\.CommandText = (?<literal>""(?:[^""\\]|\\.)*"");)?", RegexOptions.Singleline)]
    private static partial Regex Method();

    private sealed record Legacy(string Entity, string Method, string Text);

    private static IReadOnlyList<Legacy> LegacyStatements(string set)
    {
        var text = File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), (Frozen + set + ".entities.golden").Replace('/', Path.DirectorySeparatorChar)));
        var found = new List<Legacy>();
        foreach (Match block in ClassBlock().Matches(text))
            foreach (var part in Regex.Split(block.Groups["body"].Value, @"(?=\r?\n    public static async Task)"))
            {
                var header = Regex.Match(part, @"public static async Task(?:<[^\r\n]*>)? (?<name>\w+)\(DbConnection conn");
                var literal = Regex.Match(part, @"cmd\.CommandText = (?<literal>""(?:[^""\\]|\\.)*"");");
                if (header.Success && literal.Success)
                    found.Add(new Legacy(block.Groups["type"].Value, header.Groups["name"].Value, Regex.Unescape(literal.Groups["literal"].Value[1..^1])));
            }
        return found;
    }

    private static GeneratedModel ModelOf(string set)
    {
        var (selected, _) = GeneratorCases.Sets.Single(s => s.Set.Name == set);
        return GeneratedModel.Of(CorpusGenerator.Run(selected, RuntimeSources.LibraryRoot()).Output);
    }

    private static string TextOf(global::Neemle.Orm.Lite.Nodes.Node statement) =>
        StatementCompiler.Compile(statement, PostgresProvider.Instance, StatementKind.Write).Text;

    private static string Expected(EntityDescriptor d, string method)
    {
        var values = new object?[d.Columns.Count];
        if (method == "InsertAsync") return TextOf(EntityStatements.Insert(d, values));
        if (method == "GetAsync") return TextOf(EntityStatements.Get(d, Guid.Empty));
        if (method == "UpdateAsync") return TextOf(EntityStatements.Update(d, values));
        if (method == "DeleteAsync") return TextOf(EntityStatements.Delete(d, Guid.Empty));
        var verb = method.StartsWith("Load", StringComparison.Ordinal) ? "Load" : method.StartsWith("Add", StringComparison.Ordinal) ? "Add" : "Remove";
        var name = method[verb.Length..^"Async".Length];
        var link = d.ManyToMany.Single(l => l.Navigation == name || l.Navigation.TrimEnd('s') == name);
        return verb switch
        {
            "Load" => TextOf(EntityStatements.LoadLinked(d, link, Guid.Empty)),
            "Add" => TextOf(EntityStatements.AddLink(link, Guid.Empty, Guid.Empty)),
            _ => TextOf(EntityStatements.RemoveLink(link, Guid.Empty, Guid.Empty)),
        };
    }

    [Theory(DisplayName = "every statement the legacy generated repositories embedded is the statement the descriptor-built node tree compiles to on PostgreSQL, character for character, parameter placeholders included")]
    [InlineData("corpus-entities", "CorpusEntities")]
    [InlineData("golden-fixtures", "OrmLite.GoldenFixtures")]
    [InlineData("demo", "OrmLiteDemo")]
    public void The_descriptor_statements_equal_the_legacy_literals(string set, string assembly)
    {
        var legacy = LegacyStatements(set);
        var model = ModelOf(set);
        Assert.NotEmpty(legacy);
        Assert.NotNull(assembly);

        var problems = new List<string>();
        foreach (var statement in legacy)
        {
            var actual = Expected(model.Descriptor(statement.Entity), statement.Method);
            if (actual != statement.Text) problems.Add(statement.Entity + "." + statement.Method + "\n  legacy: " + statement.Text + "\n  nodes:  " + actual);
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact(DisplayName = "the legacy census covers every statement kind: insert, get, update, hard delete, the many-to-many load, add and remove")]
    public void The_census_covers_every_statement_kind()
    {
        var methods = LegacyStatements("corpus-entities").Concat(LegacyStatements("golden-fixtures")).Select(s => s.Method).Distinct().ToList();

        Assert.Contains("InsertAsync", methods);
        Assert.Contains("GetAsync", methods);
        Assert.Contains("UpdateAsync", methods);
        Assert.Contains("DeleteAsync", methods);
        Assert.Contains(methods, m => m.StartsWith("Load", StringComparison.Ordinal));
        Assert.Contains(methods, m => m.StartsWith("Add", StringComparison.Ordinal));
        Assert.Contains(methods, m => m.StartsWith("Remove", StringComparison.Ordinal));
        Assert.True(LegacyStatements("corpus-entities").Count + LegacyStatements("golden-fixtures").Count + LegacyStatements("demo").Count > 40);
    }
}
