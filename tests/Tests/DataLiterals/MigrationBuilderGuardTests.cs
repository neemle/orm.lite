using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.DataLiterals;

public sealed class MigrationBuilderGuardTests
{
    private static readonly ISqlProvider[] Providers = [PostgresProvider.Instance, SqliteProvider.Instance];

    private static readonly EntityDescriptor Probe =
        EntityDescriptor.Of("Probe", "probes", true).Property("Label", "label", ColumnType.Text, true, false);

    private static MigrationBuilder Busy(ISqlProvider provider) => new MigrationBuilder(provider).DropTable("earlier");

    [Fact(DisplayName = "an InsertData without a row is refused naming the rows, before any operation is recorded")]
    public void An_insert_without_rows_is_refused()
    {
        foreach (var provider in Providers)
        {
            var builder = Busy(provider);

            var error = Assert.Throws<ArgumentException>(() => builder.InsertData("t", ["g"]));

            Assert.Equal("rows", error.ParamName);
            Assert.Single(builder.Nodes);
        }
    }

    [Fact(DisplayName = "an InsertData without a column is refused naming the columns, before any operation "
        + "is recorded")]
    public void An_insert_without_columns_is_refused()
    {
        foreach (var provider in Providers)
        {
            var builder = Busy(provider);

            var error = Assert.Throws<ArgumentException>(
                () => builder.InsertData("t", Array.Empty<string>(), new object?[0]));

            Assert.Equal("columns", error.ParamName);
            Assert.Single(builder.Nodes);
        }
    }

    [Fact(DisplayName = "a DeleteData with a null among its values is refused naming the values and saying "
        + "the list never matches NULL, before any operation is recorded")]
    public void A_delete_with_a_null_value_is_refused()
    {
        foreach (var provider in Providers)
        {
            var builder = Busy(provider);

            var alone = Assert.Throws<ArgumentException>(() => builder.DeleteData("t", "c", (object?)null));
            var among = Assert.Throws<ArgumentException>(
                () => builder.DeleteData("t", "c", new object?[] { "x", null }));

            Assert.Equal("values", alone.ParamName);
            Assert.Equal("values", among.ParamName);
            Assert.Contains("NULL", alone.Message, StringComparison.Ordinal);
            Assert.Contains("NULL", among.Message, StringComparison.Ordinal);
            Assert.Single(builder.Nodes);
        }
    }

    [Fact(DisplayName = "[regression pin] a DeleteData without a value keeps its own refusal, checked before "
        + "the null rule")]
    public void A_delete_without_values_keeps_its_refusal()
    {
        foreach (var provider in Providers)
        {
            var error = Assert.Throws<ArgumentException>(() => new MigrationBuilder(provider).DeleteData("t", "c"));

            Assert.Equal("DeleteData needs at least one value to match. (Parameter 'values')", error.Message);
        }
    }

    [Fact(DisplayName = "the entity overload refuses a call without rows like the column overload, and keeps "
        + "recording what it recorded before for a row")]
    public void The_entity_overload_refuses_no_rows()
    {
        foreach (var provider in Providers)
        {
            var builder = Busy(provider);

            var error = Assert.Throws<ArgumentException>(() => builder.InsertData<object>(Probe, v => [v]));

            Assert.Equal("rows", error.ParamName);
            Assert.Single(builder.Nodes);
            Assert.Equal(
                "INSERT INTO \"probes\" (\"label\") VALUES ('a')",
                Assert.Single(new MigrationBuilder(provider).InsertData<object>(Probe, v => [v], "a").Render(provider)));
        }
    }

    [Fact(DisplayName = "a valid InsertData and DeleteData still record exactly one operation each")]
    public void A_valid_call_still_records_one_operation()
    {
        foreach (var provider in Providers)
        {
            var builder = new MigrationBuilder(provider).InsertData("t", ["g"], ["a"]).DeleteData("t", "g", "a");

            Assert.Equal(2, builder.Nodes.Count);
        }
    }
}
