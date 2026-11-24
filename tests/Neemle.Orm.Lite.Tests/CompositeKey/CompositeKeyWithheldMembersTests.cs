using System.Reflection;
using Xunit;
using static Neemle.Orm.Lite.Tests.CompositeKey.CompositeKeySources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.CompositeKey;

public sealed class CompositeKeyWithheldMembersTests
{
    [Theory(
        DisplayName = "AC4 [SEC] a refused entity has no get, update or delete by key in either form, read from "
        + "the compiled model and not from the diagnostics, so no severity setting can bring the members back")]
    [InlineData(Line, "Line")]
    [InlineData(CodeAndId, "Coded")]
    [InlineData(ThreeKeys, "Slot")]
    [InlineData(TwoKeysAndId, "Stamp")]
    [InlineData(KeyIdAndOther, "Twin")]
    [InlineData(SoftTwoKeys, "SoftPair")]
    public void A_refused_entity_keeps_only_the_members_that_need_no_key(string source, string entity)
    {
        var (_, _, output) = Run(source);

        var methods = GeneratedModel.Of(output).Class(entity + "Orm")
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .ToArray();

        Assert.Equal(
            ["Column", "InsertAsync", "Map", "Query", "Values"],
            methods.Select(m => m.Name).Distinct().Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, methods.Count(m => m.Name == "InsertAsync"));
    }

    [Theory(
        DisplayName = "AC4 the descriptor of a refused entity keeps the key flag of every key property: it stays "
        + "the declared schema")]
    [InlineData(Line, "Line", "OrderId,ProductId")]
    [InlineData(CodeAndId, "Coded", "Code,Id")]
    [InlineData(TwoKeysAndId, "Stamp", "Region,Id,Code")]
    public void The_descriptor_keeps_every_key_flag(string source, string entity, string keys)
    {
        var (_, _, output) = Run(source);

        var flagged = GeneratedModel.Of(output).Descriptor(entity).Columns.Where(c => c.Key).Select(c => c.Property);

        Assert.Equal(keys.Split(','), flagged);
    }

    [Fact(
        DisplayName = "AC4 the mapping of a refused entity still names the first key column in the text of an "
        + "enum read error")]
    public void The_map_of_a_refused_entity_keeps_the_first_key_column()
    {
        var (_, generated, _) = Run(EnumLine);

        Assert.Contains(
            "OrmEnum.Read<global::Fixture.Level>(r, o2, \"tones\", \"level\", \"order_id\")",
            generated,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC4 a refused [SoftDelete] entity keeps its purge class")]
    public void A_refused_soft_entity_keeps_its_purge_class()
    {
        var (_, _, output) = Run(SoftTwoKeys);

        var model = GeneratedModel.Of(output);

        Assert.True(model.Has("SoftPairPurge"));
        Assert.NotNull(model.Descriptor("SoftPair").Soft);
    }
}
