using System.Text.RegularExpressions;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.LiveOnlyIndex;

public sealed class IndexSetHashGeneratorTests
{
    private static string Entity(string indexes) =>
        $"[Table(\"docs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] {indexes} public class Doc "
        + "{ [Key] public Guid Id { get; set; } public string Scope { get; set; } = \"\"; public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

    private static string HashOf(string source)
    {
        var (diagnostics, generated, _) = Run(source);

        Assert.Empty(diagnostics);
        return Regex.Match(generated, "IndexSetHash = \"([0-9a-f]{16})\"").Groups[1].Value;
    }

    [Fact(DisplayName = "AC4: the same entity declared with and without LiveOnly yields a different index-set hash")]
    public void Adding_live_only_changes_the_index_set_hash()
    {
        var plain = HashOf(Entity("[Index(\"Scope\", \"Name\", Unique = true)]"));
        var live = HashOf(Entity("[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)]"));

        Assert.Equal(16, plain.Length);
        Assert.Equal(16, live.Length);
        Assert.NotEqual(plain, live);
    }

    [Fact(DisplayName = "AC4: two declarations that differ only by LiveOnly on an identical explicit index Name yield a different index-set hash")]
    public void Live_only_alone_changes_the_hash_of_an_identically_named_index()
    {
        var total = HashOf(Entity("[Index(\"Scope\", \"Name\", Unique = true, Name = \"uq_docs_scope_name\")]"));
        var live = HashOf(Entity("[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true, Name = \"uq_docs_scope_name\")]"));

        Assert.Equal(16, total.Length);
        Assert.Equal(16, live.Length);
        Assert.NotEqual(total, live);
    }
}
