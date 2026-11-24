using System.Text.RegularExpressions;
using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class InitOnlyEntityGeneratorTests
{
    private static string MapFlat(string source, string entity = "A")
    {
        var (diagnostics, generated, _) = Built(source);
        Assert.Empty(diagnostics);
        return MapOf(generated, entity);
    }

    [Theory(
        DisplayName = "AC1 an init-only or required column compiles with no diagnostic and is built in the "
        + "initializer")]
    [InlineData(InitNonKey, "Title = r.GetString(o1)", "e.Id = r.GetGuid(o0);")]
    [InlineData(InitKey, "Id = r.GetGuid(o0)", "e.Title = r.GetString(o1);")]
    [InlineData(RequiredWithSet, "Title = r.GetString(o1)", "e.Id = r.GetGuid(o0);")]
    [InlineData(RequiredInit, "Title = r.GetString(o1)", "e.Id = r.GetGuid(o0);")]
    [InlineData(RequiredNullable, "Note = r.IsDBNull(o1) ? null : r.GetString(o1)", "e.Id = r.GetGuid(o0);")]
    [InlineData(SoftInit, "DeletedAt = r.IsDBNull(o2) ? null : r.GetDateTime(o2)", "e.Title = r.GetString(o1);")]
    public void An_init_or_required_column_is_built_in_the_initializer(
        string source, string initialised, string statement)
    {
        var map = MapFlat(source);

        Assert.Contains("new global::Fixture.A() { " + initialised + " }", map, StringComparison.Ordinal);
        Assert.Contains(statement, map, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "AC1 the ordinals are read before the object is constructed")]
    [InlineData(InitNonKey)]
    [InlineData(RequiredWithSet)]
    [InlineData(Mixed)]
    public void The_ordinals_come_before_the_construction(string source)
    {
        var map = MapFlat(source);

        Assert.True(
            map.IndexOf("var o1 =", StringComparison.Ordinal) < map.IndexOf("var e = new", StringComparison.Ordinal));
        Assert.Equal(1, Regex.Count(map, @"\bvar e = new\b"));
    }

    [Fact(DisplayName = "AC1 a mix of init, required and plain columns reads every column exactly once")]
    public void A_mixed_entity_reads_every_column_once()
    {
        var map = MapFlat(Mixed);

        Assert.Contains(
            "new global::Fixture.A() { Code = r.GetString(o1), Title = r.GetString(o2), Score = r.GetInt32(o4) }",
            map,
            StringComparison.Ordinal);
        Assert.Contains("e.Id = r.GetGuid(o0);", map, StringComparison.Ordinal);
        Assert.Contains("e.Note = r.GetString(o3);", map, StringComparison.Ordinal);
        for (var i = 0; i < 5; i++)
            Assert.Equal(1, Regex.Count(map, @"\(o" + i + @"\)"));
    }

    [Fact(DisplayName = "AC1 a view with required and init columns builds them in the initializer")]
    public void A_view_builds_required_and_init_columns_in_the_initializer()
    {
        var map = MapFlat(ViewWithRequiredAndInit);

        Assert.Contains(
            "new global::Fixture.A() { Title = r.GetString(o1), Note = r.GetString(o2) }",
            map,
            StringComparison.Ordinal);
        Assert.Contains("e.Id = r.GetGuid(o0);", map, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC1 enum, Jsonb and nullable columns read as they do for a setter, as init or required")]
    public void Enum_jsonb_and_nullable_columns_keep_their_read_expression()
    {
        var map = MapFlat(EnumJsonbAndNullable);

        Assert.Contains(
            "Level = OrmEnum.Read<global::Fixture.Level>(r, o1, \"a\", \"level\", \"id\")",
            map,
            StringComparison.Ordinal);
        Assert.Contains(
            "Prior = r.IsDBNull(o2) ? null : OrmEnum.Read<global::Fixture.Level>(r, o2, \"a\", \"prior\", \"id\")",
            map,
            StringComparison.Ordinal);
        Assert.Contains("Body = Jsonb.FromStorage(r.GetString(o3))", map, StringComparison.Ordinal);
        Assert.Contains(
            "Extra = r.IsDBNull(o4) ? null : Jsonb.FromStorage(r.GetString(o4))",
            map,
            StringComparison.Ordinal);
        Assert.Contains("Count = r.IsDBNull(o5) ? null : r.GetInt32(o5)", map, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "AC1 a required column under a parameterless constructor with SetsRequiredMembers stays a "
        + "statement")]
    public void A_required_column_under_the_attribute_stays_a_statement()
    {
        var map = MapFlat(SetsRequiredOnParameterless);

        Assert.DoesNotContain("() {", map, StringComparison.Ordinal);
        Assert.Contains("var e = new global::Fixture.A();", map, StringComparison.Ordinal);
        Assert.Contains("e.Title = r.GetString(o1);", map, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "AC2 the Map of that entity equals the Map of the same entity without required, after the "
        + "type name")]
    public void The_pair_has_equal_map_text()
    {
        var (_, withAttribute, _) = Run(SetsRequiredOnParameterless);
        var (_, plain, _) = Run(PlainTwin);

        var left = MapOf(withAttribute, "A");
        var right = MapOf(plain, "A");

        Assert.Equal(right, left);
        Assert.Contains(
            "var e = new global::Fixture.A(); var o0 = r.GetOrdinal(\"id\"); var o1 = r.GetOrdinal(\"title\");",
            left,
            StringComparison.Ordinal);
        Assert.Contains(
            "e.Id = r.GetGuid(o0); e.Title = r.GetString(o1); e.Score = r.GetInt32(o2); return e; }",
            left,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC2 the attribute counts only on the parameterless constructor")]
    public void The_attribute_on_a_parameterised_constructor_does_not_count()
    {
        var map = MapFlat(SetsRequiredOnParameterisedOnly);

        Assert.Contains("new global::Fixture.A() { Title = r.GetString(o1) }", map, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC2 the attribute is matched by its full name, not by its simple name")]
    public void A_same_named_attribute_of_another_type_does_not_count()
    {
        var map = MapFlat(SameNamedAttribute);

        Assert.Contains("new global::Fixture.A() { Title = r.GetString(o1) }", map, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "AC2 an entity with no init-only and no required column keeps the construction statement")]
    [InlineData(PlainTwin)]
    [InlineData(Sibling)]
    public void A_plain_entity_keeps_the_statement_form(string source)
    {
        var (_, generated, _) = Run(source);
        var entity = source.Contains("class Sib", StringComparison.Ordinal) ? "Sib" : "A";

        var map = MapOf(generated, entity);

        Assert.Contains("var e = new global::Fixture." + entity + "(); var o0", map, StringComparison.Ordinal);
        Assert.DoesNotContain("() {", map, StringComparison.Ordinal);
    }
}
