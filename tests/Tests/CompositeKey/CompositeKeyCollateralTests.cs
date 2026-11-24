using System.Reflection;
using Xunit;
using static Neemle.OrmLite.Tests.CompositeKey.CompositeKeySources;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.CompositeKey;

public sealed class CompositeKeyCollateralTests
{
    private static string[] Members(string source, string entity)
    {
        var (_, _, output) = Run(source);
        return GeneratedModel.Of(output).Class(entity + "Orm")
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    [Theory(
        DisplayName = "AC4 [SEC] an entity that points at a refused entity gets no loader for that relation, and "
        + "the generated file still compiles")]
    [InlineData(NoteWithRelation)]
    [InlineData(NoteWithNullableRelation)]
    public void A_relation_to_a_refused_entity_has_no_loader(string source)
    {
        var members = Members(source, "Note");

        Assert.DoesNotContain("LoadPairAsync", members);
        Assert.Equal(
            ["Column", "DeleteAsync", "GetAsync", "InsertAsync", "Map", "Query", "UpdateAsync", "Values"],
            members);
    }

    [Fact(
        DisplayName = "AC4 [SEC] a refused entity has no loader for the collection it owns, and the generated "
        + "file still compiles")]
    public void A_collection_of_a_refused_entity_has_no_loader()
    {
        var members = Members(PairWithCollection, "Pair");

        Assert.DoesNotContain("LoadPartsAsync", members);
        Assert.Equal(["Column", "InsertAsync", "Map", "Query", "Values"], members);
    }

    [Fact(
        DisplayName = "AC4 [SEC] a refused entity has no load, add or remove for its many-to-many list, and the "
        + "generated file still compiles")]
    public void A_many_to_many_of_a_refused_entity_has_no_link_members()
    {
        var members = Members(PairWithLinks, "Pair");

        Assert.DoesNotContain("LoadTagsAsync", members);
        Assert.DoesNotContain("AddTagAsync", members);
        Assert.DoesNotContain("RemoveTagAsync", members);
        Assert.Equal(["Column", "InsertAsync", "Map", "Query", "Values"], members);
    }

    [Fact(
        DisplayName = "AC4 [SEC] an entity whose many-to-many other side is refused has no load, add or remove "
        + "for it, and the generated file still compiles")]
    public void A_many_to_many_towards_a_refused_entity_has_no_link_members()
    {
        var members = Members(AuthorLinkedToPairs, "Author");

        Assert.DoesNotContain("LoadPairsAsync", members);
        Assert.DoesNotContain("AddPairAsync", members);
        Assert.DoesNotContain("RemovePairAsync", members);
        Assert.Equal(["Column", "DeleteAsync", "GetAsync", "InsertAsync", "Map", "Query", "Values"], members);
    }

    [Fact(
        DisplayName = "AC4 a refused entity that points at itself has no loader for that relation, and the "
        + "generated file still compiles")]
    public void A_self_relation_of_a_refused_entity_has_no_loader()
    {
        var members = Members(NodeWithParent, "Node");

        Assert.DoesNotContain("LoadParentAsync", members);
        Assert.Equal(["Column", "InsertAsync", "Map", "Query", "Values"], members);
    }

    [Fact(
        DisplayName = "AC4 pin: an owner of a collection of refused entities keeps its loader, because it reads "
        + "the children through their query by the owner's key")]
    public void A_collection_of_refused_children_keeps_its_loader()
    {
        var members = Members(AuthorWithPairCollection, "Author");

        Assert.Contains("LoadPairsAsync", members);
    }

    [Fact(DisplayName = "AC4 pin: a refused entity keeps the loader of a relation whose target is not refused")]
    public void A_refused_entity_keeps_a_relation_to_an_ordinary_target()
    {
        var members = Members(LineWithBoss, "Line");

        Assert.Contains("LoadBossAsync", members);
        Assert.DoesNotContain("GetAsync", members);
        Assert.DoesNotContain("UpdateAsync", members);
        Assert.DoesNotContain("DeleteAsync", members);
    }

    [Fact(
        DisplayName = "AC4 pin: the relations, collections and many-to-many lists of ordinary entities are "
        + "unchanged next to a refused entity")]
    public void Ordinary_entities_next_to_a_refused_one_keep_every_member()
    {
        Assert.Equal(
            [
                "AddTagAsync", "Column", "DeleteAsync", "GetAsync", "InsertAsync", "LoadBooksAsync", "LoadTagsAsync",
                "Map", "Query", "RemoveTagAsync", "UpdateAsync", "Values",
            ],
            Members(OrdinaryNeighbours, "Author"));
        Assert.Equal(
            [
                "Column", "DeleteAsync", "GetAsync", "InsertAsync", "LoadAuthorAsync", "Map", "Query", "UpdateAsync",
                "Values",
            ],
            Members(OrdinaryNeighbours, "Book"));
        Assert.Equal(["Column", "InsertAsync", "Map", "Query", "Values"], Members(OrdinaryNeighbours, "Pair"));
    }
}
