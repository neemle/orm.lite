using System.Reflection;
using Xunit;
using static Neemle.OrmLite.Tests.CompositeKey.CompositeKeySources;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.CompositeKey;

public sealed class CompositeKeyNoRefusalTests
{
    private static string[] Members(Microsoft.CodeAnalysis.Compilation output, string entity) =>
        GeneratedModel.Of(output).Class(entity + "Orm")
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Theory(
        DisplayName = "AC3 [SEC] an entity with one key is not refused and keeps its keyed members: a [Key] on "
        + "Id, a [Key] on another property, a key named Id without [Key], a [Key] on a [NotAColumn] property next "
        + "to one key")]
    [InlineData(KeyOnId, "Doc")]
    [InlineData(KeyElsewhere, "Register")]
    [InlineData(IdByNameOnly, "Thing")]
    [InlineData(ShadowKey, "Shadowed")]
    public void A_single_key_keeps_the_keyed_members(string source, string entity)
    {
        var (diagnostics, _, output) = Run(source);

        Assert.Empty(diagnostics);
        Assert.Equal(
            [
                "Column", "DeleteAsync", "DeleteAsync", "GetAsync", "GetAsync", "InsertAsync", "InsertAsync", "Map",
                "Query", "UpdateAsync", "UpdateAsync", "Values",
            ],
            Members(output, entity));
    }

    [Fact(DisplayName = "AC3 an entity with no key is not refused and still emits no member that takes a key")]
    public void A_keyless_entity_is_not_refused()
    {
        var (diagnostics, _, output) = Run(Keyless);

        Assert.Empty(diagnostics);
        Assert.Equal(["Column", "InsertAsync", "InsertAsync", "Map", "Query", "Values"], Members(output, "Pair"));
    }

    [Fact(
        DisplayName = "AC3 a [View] with two key-flagged properties is not refused and emits the same read-only "
        + "members as any view")]
    public void A_view_with_two_keys_is_not_refused()
    {
        var (diagnostics, _, output) = Run(ViewWithTwoKeys);

        Assert.Empty(diagnostics);
        Assert.Equal(
            ["Column", "DeleteAsync", "InsertAsync", "Map", "Query", "UpdateAsync"],
            Members(output, "PairView").Distinct().ToArray());
    }

    [Fact(
        DisplayName = "AC3 a single key on a [NotAColumn] property is not a key: the entity next to it keeps "
        + "exactly the one key flag")]
    public void A_key_on_an_unmapped_property_is_not_counted()
    {
        var (_, _, output) = Run(ShadowKey);

        var keys = GeneratedModel.Of(output).Descriptor("Shadowed").Columns
            .Where(c => c.Key)
            .Select(c => c.Property)
            .ToArray();

        Assert.Equal(["Id"], keys);
    }
}
