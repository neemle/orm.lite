using Microsoft.CodeAnalysis;
using Xunit;

namespace Neemle.Orm.Lite.Tests.IdentifierLimits;

// ORML-29 (B42): the generator names the entity and the name when a provider of the project keeps fewer bytes.
public sealed class IdentifierDiagnosticsTests
{
    private static string Entity(string table, string indexName) =>
        "[Table(\"" + table + "\")] [Index(\"Name\", Name = \"" + indexName + "\")] public class Doc "
        + "{ [Key] public Guid Id { get; set; } public string Name { get; set; } = \"\"; }\n";

    private static string Bytes(int count) => new('x', count);

    private static List<string> Messages(string source, string manifest) =>
        ManifestHarness.Run(source, "postgres", manifest).Select(d => d.GetMessage()).ToList();

    [Fact(DisplayName = "ORMLITE034 names the entity, the kind, the name, its length, the provider and the limit "
        + "for an index name of 64 bytes when the manifest states 63")]
    public void A_long_index_name_is_an_error()
    {
        var all = ManifestHarness.Run(Entity("docs", Bytes(64)), "postgres", ManifestHarness.Manifest("63"));
        var found = Assert.Single(all);
        var message = found.GetMessage();

        Assert.Equal(DiagnosticSeverity.Error, found.Severity);
        Assert.Contains("'Doc'", message, StringComparison.Ordinal);
        Assert.Contains("index name '" + Bytes(64) + "'", message, StringComparison.Ordinal);
        Assert.Contains("64 bytes", message, StringComparison.Ordinal);
        Assert.Contains("'postgres' keeps at most 63", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE034 is not raised for names of exactly 63 bytes, for a manifest without a limit "
        + "or for one with a bad limit")]
    public void A_name_within_the_limit_or_no_limit_raises_nothing()
    {
        var long200 = Entity("docs", Bytes(200));

        Assert.Empty(Messages(Entity(Bytes(63), Bytes(63)), ManifestHarness.Manifest("63")));
        Assert.Empty(Messages(long200, ManifestHarness.Manifest(null)));
        Assert.Empty(Messages(long200, ManifestHarness.Manifest("many")));
    }

    [Fact(DisplayName = "the default name of an index over a long column, with its _live suffix, is reported "
        + "as an index name over the limit")]
    public void A_default_index_name_that_grows_past_the_limit_is_an_error()
    {
        var column = "Customer" + new string('A', 60) + "Reference";
        var source = "[Table(\"orders\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] [Index(\"" + column
            + "\", Unique = true, LiveOnly = true)] public class Order { [Key] public Guid Id { get; set; } "
            + "public string " + column + " { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

        var messages = Messages(source, ManifestHarness.Manifest("63"));

        Assert.Contains(messages, m => m.Contains("index name 'ix_orders_", StringComparison.Ordinal)
            && m.Contains("_live'", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("column name", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "a table name and a column name over the limit are named with their kind")]
    public void A_long_table_and_column_are_errors()
    {
        var source = "[Table(\"" + Bytes(70) + "\")] public class Doc { [Key] public Guid Id { get; set; } "
            + "public string C" + Bytes(70) + " { get; set; } = \"\"; }\n";

        var messages = Messages(source, ManifestHarness.Manifest("63"));

        Assert.Contains(messages, m => m.Contains("table name '" + Bytes(70) + "'", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("column name '", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "the shipped PostgreSQL manifest carries the limit the generator reads, so a 64-byte "
        + "index name is refused with the shipped file")]
    public void The_shipped_manifest_drives_the_check()
    {
        var assets = Path.Combine(RuntimeSources.LibraryRoot(), "src", "Neemle.Orm.Lite.Postgres", "assets");
        var path = Path.Combine(assets, "ormlite.capabilities.postgres.txt");

        var found = Messages(Entity("docs", Bytes(64)), File.ReadAllText(path));

        Assert.Single(found);
    }
}
