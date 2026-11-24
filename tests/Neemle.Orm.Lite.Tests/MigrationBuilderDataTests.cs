using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class MigrationBuilderDataTests
{
    private const string Id = "00000000-0000-0000-0000-00000000d3a0";

    [Fact]
    public void InsertData_renders_one_row_with_quoted_identifiers_and_dialect_literals()
    {
        var builder = new MigrationBuilder();
        builder.InsertData("notes", ["id", "title", "stars", "pinned", "body"], [Id, "it's", 3, true, null]);

        Assert.Equal(
            "INSERT INTO \"notes\" (\"id\", \"title\", \"stars\", \"pinned\", \"body\") VALUES ('" + Id + "', 'it''s', 3, true, NULL)",
            Assert.Single(builder.Operations));
    }

    [Fact]
    public void InsertData_renders_several_rows_in_one_statement()
    {
        var builder = new MigrationBuilder();
        builder.InsertData("tags", ["name", "weight"], ["a", 1], ["b", 2]);

        Assert.Equal(
            "INSERT INTO \"tags\" (\"name\", \"weight\") VALUES ('a', 1), ('b', 2)",
            Assert.Single(builder.Operations));
    }

    [Fact]
    public void DeleteData_renders_a_delete_that_matches_the_column_against_the_value()
    {
        var builder = new MigrationBuilder();
        builder.DeleteData("notes", "id", Id);

        Assert.Equal("DELETE FROM \"notes\" WHERE \"id\" IN ('" + Id + "')", Assert.Single(builder.Operations));
    }

    [Fact]
    public void DeleteData_matches_several_values_and_escapes_quotes_in_values_and_identifiers()
    {
        var builder = new MigrationBuilder();
        builder.DeleteData("odd\"table", "na\"me", "o'brien", 7);

        Assert.Equal(
            "DELETE FROM \"odd\"\"table\" WHERE \"na\"\"me\" IN ('o''brien', 7)",
            Assert.Single(builder.Operations));
    }

    [Fact]
    public void DeleteData_without_a_value_is_refused_because_it_would_render_invalid_SQL()
    {
        var builder = new MigrationBuilder();

        Assert.Throws<ArgumentException>(() => builder.DeleteData("notes", "id"));
        Assert.Empty(builder.Operations);
    }

    [Fact]
    public void DeleteData_is_chainable_and_follows_earlier_operations_in_order()
    {
        var builder = new MigrationBuilder();
        builder.InsertData("notes", ["id"], [Id]).DeleteData("notes", "id", Id);

        Assert.Equal(2, builder.Operations.Count);
        Assert.StartsWith("INSERT INTO", builder.Operations[0]);
        Assert.StartsWith("DELETE FROM", builder.Operations[1]);
    }

    private sealed record Seed(Guid Id, string Title, int Stars, DateTime At, Jsonb Doc, Kind Kind, bool? Pinned);

    private enum Kind
    {
        Draft,
    }

    private static readonly EntityDescriptor SeedTable = EntityDescriptor.Of("Seed", "seeds", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("Title", "title", ColumnType.Text, false, false)
        .Property("Stars", "stars", ColumnType.Int32, false, false)
        .Property("At", "at", ColumnType.Timestamp, false, false)
        .Property("Doc", "doc", ColumnType.Json, false, false)
        .Property("Kind", "kind", ColumnType.Text, false, false)
        .Property("Pinned", "pinned", ColumnType.Bool, true, false);

    private static object?[] SeedValues(Seed s) => OrmExec.Row(s.Id, s.Title, s.Stars, s.At, s.Doc, s.Kind, s.Pinned);

    [Fact]
    public void InsertData_of_entity_objects_renders_one_row_per_object_with_quoted_guid_timestamp_document_and_enum()
    {
        var builder = new MigrationBuilder();
        var first = new Seed(Guid.Parse(Id), "it's", 3, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), Jsonb.Parse("{\"k\":1}"), Kind.Draft, true);
        var second = first with { Title = "two", Pinned = null };

        builder.InsertData(SeedTable, SeedValues, first, second);

        Assert.Equal(
            "INSERT INTO \"seeds\" (\"id\", \"title\", \"stars\", \"at\", \"doc\", \"kind\", \"pinned\") VALUES "
            + "('" + Id + "', 'it''s', 3, '2026-01-02T03:04:05.0000000Z', '{\"k\":1}', 'Draft', true), "
            + "('" + Id + "', 'two', 3, '2026-01-02T03:04:05.0000000Z', '{\"k\":1}', 'Draft', NULL)",
            Assert.Single(builder.Operations));
    }

    [Fact]
    public void InsertData_of_entity_objects_refuses_a_missing_descriptor_or_projection()
    {
        var builder = new MigrationBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.InsertData<Seed>(null!, SeedValues));
        Assert.Throws<ArgumentNullException>(() => builder.InsertData<Seed>(SeedTable, null!));
        Assert.Empty(builder.Operations);
    }
}
