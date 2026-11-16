using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.LiveOnlyIndex;

public sealed class CollisionBlockGeneratorTests
{
    private const string CollisionMarker = "DO $collision$";

    private static string Entity(string indexes) =>
        $"[Table(\"live_only_scoped\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] {indexes} public class Doc "
        + "{ [Key] public Guid Id { get; set; } public string Scope { get; set; } = \"\"; public string Name { get; set; } = \"\"; public string Code { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

    private static Type SchemaOf(Compilation output)
    {
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        return assembly.GetType("Neemle.Orm.Lite.Generated." + output.AssemblyName + "Schema", throwOnError: true)!;
    }

    private static string Fnv1a64(IEnumerable<string> statements)
    {
        unchecked
        {
            var hash = 14695981039346656037UL;
            foreach (var statement in statements.OrderBy(s => s, StringComparer.Ordinal))
            {
                foreach (var c in statement)
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
                hash ^= '\n';
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16");
        }
    }

    [Fact(DisplayName = "AC5: the reconcile list of a LiveOnly index holds exactly one collision block, immediately after that index's guard statement, naming the table, the key columns and the live index")]
    public void A_live_only_index_is_followed_by_one_collision_block()
    {
        var (diagnostics, _, output) = Run(Entity("[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)] [Index(\"Scope\", \"Code\", Unique = true)]"));
        Assert.Empty(diagnostics);

        var reconcile = (string[])SchemaOf(output).GetField("ReconcileIndexes")!.GetValue(null)!;

        var block = Assert.Single(reconcile, s => s.Contains(CollisionMarker, StringComparison.Ordinal));
        var at = Array.IndexOf(reconcile, block);
        Assert.StartsWith("DO $reconcile$", reconcile[at - 1], StringComparison.Ordinal);
        Assert.Contains("ix_live_only_scoped_scope_name_live", reconcile[at - 1], StringComparison.Ordinal);
        Assert.Contains("ARRAY['scope', 'name'])) = 2", block, StringComparison.Ordinal);
        Assert.Contains("ARRAY['scope', 'name', 'deleted_at'])) = 3", block, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", block, StringComparison.Ordinal);
        Assert.Contains("'ix_live_only_scoped_scope_name_live', 'live_only_scoped', 'scope, name'", block, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC5: an entity with no LiveOnly index has no collision block in its reconcile list")]
    public void A_plain_index_has_no_collision_block()
    {
        var (diagnostics, _, output) = Run(Entity("[Index(\"Scope\", \"Name\", Unique = true)] [Index(\"Scope\", \"Code\")]"));
        Assert.Empty(diagnostics);

        var reconcile = (string[])SchemaOf(output).GetField("ReconcileIndexes")!.GetValue(null)!;

        Assert.NotEmpty(reconcile);
        Assert.DoesNotContain(reconcile, s => s.Contains(CollisionMarker, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "AC5: the collision block is not part of the hashed index set, which is the hash of the create statements alone")]
    public void The_index_set_hash_ignores_the_collision_block()
    {
        var (diagnostics, _, output) = Run(Entity("[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)] [Index(\"Scope\", \"Code\", Unique = true)]"));
        Assert.Empty(diagnostics);
        var schema = SchemaOf(output);

        var creates = (string[])schema.GetField("CreateIndexes")!.GetValue(null)!;
        var reconcile = (string[])schema.GetField("ReconcileIndexes")!.GetValue(null)!;

        Assert.Contains(reconcile, s => s.Contains(CollisionMarker, StringComparison.Ordinal));
        Assert.Equal(Fnv1a64(creates), (string)schema.GetField("IndexSetHash")!.GetRawConstantValue()!);
    }
}
