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
    [InlineData(KeyOnId, "Doc", typeof(Guid))]
    [InlineData(KeyElsewhere, "Register", typeof(string))]
    [InlineData(IdByNameOnly, "Thing", typeof(Guid))]
    [InlineData(ShadowKey, "Shadowed", typeof(Guid))]
    public void A_single_key_keeps_the_keyed_members(string source, string entity, Type keyType)
    {
        var (diagnostics, _, output) = Run(source);

        Assert.Empty(diagnostics);
        Assert.Equal(
            [
                "Column", "DeleteAsync", "DeleteAsync", "GetAsync", "GetAsync", "GetAsync", "GetAsync",
                "InsertAsync", "InsertAsync", "Map",
                "Query", "UpdateAsync", "UpdateAsync", "Values",
            ],
            Members(output, entity));
        KeyedSignatures(output, entity, keyType);
    }

    private static void KeyedSignatures(Microsoft.CodeAnalysis.Compilation output, string entity, Type keyType)
    {
        var methods = GeneratedModel.Of(output).Class(entity + "Orm")
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "GetAsync").ToArray();
        Assert.All(methods, method =>
        {
            Assert.False(method.IsGenericMethod);
            Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
            Assert.Equal("Fixture." + entity, method.ReturnType.GenericTypeArguments.Single().FullName);
            Assert.True(method.GetParameters()[^1].IsOptional);
            Assert.True(method.GetParameters()[^1].HasDefaultValue);
            Assert.All(method.GetParameters()[..^1], parameter => Assert.False(parameter.IsOptional));
        });
        var context = typeof(OrmContext).FullName + ",";
        var connection = typeof(System.Data.Common.DbConnection).FullName + ",";
        var key = keyType.FullName + ",";
        var options = typeof(RelationReadOptions).FullName + ",";
        var ct = typeof(CancellationToken).FullName;
        Assert.Equal(new[] { context + key + ct, context + connection + key + ct,
                context + key + options + ct, context + connection + key + options + ct }.Order(StringComparer.Ordinal),
            methods.Select(m => string.Join(",", m.GetParameters().Select(p => p.ParameterType.FullName)))
                .Order(StringComparer.Ordinal));
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
