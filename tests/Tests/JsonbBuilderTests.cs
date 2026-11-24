using System.Linq;
using Neemle.OrmLite;
using Xunit;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests;

public sealed class JsonbBuilderTests
{
    [Fact]
    public void AddJsonbColumn_is_additive_and_idempotent_because_it_carries_IF_NOT_EXISTS()
    {
        var builder = new MigrationBuilder(PostgresProvider.Instance);
        builder.AddJsonbColumn("work_items", "field_values");

        var op = Assert.Single(builder.Render(PostgresProvider.Instance));

        Assert.Equal(
            "ALTER TABLE \"work_items\" ADD COLUMN IF NOT EXISTS \"field_values\" jsonb",
            op);
    }

    [Fact]
    public void A_non_nullable_jsonb_column_added_to_a_LIVE_table_carries_a_default_so_existing_rows_have_a_value()
    {
        var builder = new MigrationBuilder(PostgresProvider.Instance);
        builder.AddJsonbColumn("work_items", "field_values", nullable: false);

        Assert.Equal(
            "ALTER TABLE \"work_items\" ADD COLUMN IF NOT EXISTS \"field_values\" jsonb NOT NULL DEFAULT '{}'::jsonb",
            Assert.Single(builder.Render(PostgresProvider.Instance)));
    }

    [Fact]
    public void The_same_two_calls_run_twice_produce_the_same_two_statements_and_no_third()
    {
        var first = new MigrationBuilder(PostgresProvider.Instance);
        first.AddJsonbColumn("work_items", "field_values");

        var second = new MigrationBuilder(PostgresProvider.Instance);
        second.AddJsonbColumn("work_items", "field_values");

        Assert.Equal(first.Render(PostgresProvider.Instance), second.Render(PostgresProvider.Instance));
        Assert.Single(second.Render(PostgresProvider.Instance));
    }

    [Fact]
    public void A_created_table_can_carry_a_jsonb_column_without_the_caller_spelling_the_type()
    {
        var builder = new MigrationBuilder(PostgresProvider.Instance);
        builder.CreateTable("work_item_field_values", t => t
            .Column("work_item_id", Neemle.OrmLite.Nodes.ColumnType.Uuid, nullable: false, primaryKey: true)
            .JsonbColumn("values", nullable: false));

        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"work_item_field_values\" "
            + "(\"work_item_id\" uuid NOT NULL PRIMARY KEY, \"values\" jsonb NOT NULL DEFAULT '{}'::jsonb)",
            Assert.Single(builder.Render(PostgresProvider.Instance)));
    }

    [Fact]
    public void A_nullable_jsonb_column_in_a_created_table_carries_no_default()
    {
        var table = new TableBuilder(PostgresProvider.Instance);
        table.JsonbColumn("values");

        Assert.Equal("\"values\" jsonb", table.Render());
    }

    [Fact]
    public void The_builder_emits_jsonb_and_never_the_different_type_spelled_json()
    {
        var builder = new MigrationBuilder(PostgresProvider.Instance);
        builder.AddJsonbColumn("t", "c");

        var op = Assert.Single(builder.Render(PostgresProvider.Instance));

        Assert.Contains(" jsonb", op);
        Assert.DoesNotContain(" json ", op);
        Assert.False(op.TrimEnd().EndsWith(" json", System.StringComparison.Ordinal));
    }
}
