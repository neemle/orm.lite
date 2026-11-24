using System.Text.RegularExpressions;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class GeneratedRunnerParityTests
{
    private const string Frozen = "tests/Tests/Nodes/LegacyRunner/";

    private const string Recorded = "tests/Tests/Corpus/Goldens/";

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static List<string> Statements(string transcript, bool legacy)
    {
        var found = new List<string>();
        foreach (var chunk in Regex.Split(transcript.TrimEnd('\n'), "\n(?=#\\d+ )"))
        {
            var match = Regex.Match(chunk, "\n    sql:\n((?:        .*\n?)+)");
            if (!match.Success) continue;
            var sql = Regex.Replace(match.Groups[1].Value, "^        ", "", RegexOptions.Multiline).TrimEnd('\n');
            if (sql.Contains("schema_migrations", StringComparison.Ordinal)) continue;
            if (sql.StartsWith("DO $lc$", StringComparison.Ordinal)) continue;
            if (!legacy) { if (!sql.StartsWith("DO $drift$", StringComparison.Ordinal)) found.Add(sql); continue; }
            sql = sql.Replace("''::text", "''", StringComparison.Ordinal).Replace("numeric(12,4)", "numeric(12, 4)", StringComparison.Ordinal);
            found.AddRange(Regex.Split(sql, "; (?=CREATE TABLE)"));
        }
        return found;
    }

    [Theory(DisplayName = "the operation statements the core runner executes for the generated migrations are the statements the legacy generated runner executed, in the same order, apart from three spellings (an unnamed text default, the decimal type's comma and the baseline split into one statement per table) and the drift block the reconcile adds ahead of each index")]
    [InlineData("corpus-entities", "migration-runner-fresh-database")]
    [InlineData("corpus-entities", "migration-runner-partially-applied")]
    [InlineData("demo", "migration-runner-fresh-database")]
    [InlineData("demo", "migration-runner-partially-applied")]
    [InlineData("golden-fixtures", "migration-runner-fresh-database")]
    [InlineData("golden-fixtures", "migration-runner-partially-applied")]
    public void The_operation_statements_are_the_legacy_ones(string set, string transcript)
    {
        var legacy = Statements(Read(Frozen + set + "." + transcript + ".golden"), legacy: true);
        var current = Statements(Read(Recorded + "generator-" + set + "/" + transcript + ".golden"), legacy: false);

        Assert.NotEmpty(current);
        Assert.Equal(legacy, current);
    }

    [Fact(DisplayName = "the frozen legacy transcripts still show the unnamed text default and the compact decimal the normalisation rewrites, and the recorded ones no longer do")]
    public void The_normalisations_are_the_only_differences()
    {
        var legacy = Read(Frozen + "corpus-entities.migration-runner-fresh-database.golden");
        var current = Read(Recorded + "generator-corpus-entities/migration-runner-fresh-database.golden");

        Assert.Contains("DEFAULT ''::text", legacy, StringComparison.Ordinal);
        Assert.Contains("numeric(12,4)", legacy, StringComparison.Ordinal);
        Assert.DoesNotContain("''::text", current, StringComparison.Ordinal);
        Assert.DoesNotContain("numeric(12,4)", current, StringComparison.Ordinal);
    }
}
