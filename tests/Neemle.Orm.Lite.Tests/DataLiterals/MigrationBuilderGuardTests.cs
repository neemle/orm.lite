using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.DataLiterals;

public sealed class MigrationBuilderGuardTests
{
    private static readonly Func<MigrationBuilder>[] Builders =
    [
        () => new MigrationBuilder(PostgresProvider.Instance),
        () => new MigrationBuilder(SqliteProvider.Instance),
    ];

    private static readonly EntityDescriptor Probe =
        EntityDescriptor.Of("Probe", "probes", true).Property("Label", "label", ColumnType.Text, true, false);

    private static MigrationBuilder Busy(Func<MigrationBuilder> make) => make().DropTable("earlier");

    [Fact(DisplayName = "an InsertData without a row is refused naming the rows, before any operation is recorded")]
    public void An_insert_without_rows_is_refused()
    {
        foreach (var make in Builders)
        {
            var builder = Busy(make);

            var error = Assert.Throws<ArgumentException>(() => builder.InsertData("t", ["g"]));

            Assert.Equal("rows", error.ParamName);
            Assert.Single(builder.Nodes);
        }
    }

    [Fact(DisplayName = "an InsertData without a column is refused naming the columns, before any operation "
        + "is recorded")]
    public void An_insert_without_columns_is_refused()
    {
        foreach (var make in Builders)
        {
            var builder = Busy(make);

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
        foreach (var make in Builders)
        {
            var builder = Busy(make);

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
        foreach (var make in Builders)
        {
            var error = Assert.Throws<ArgumentException>(() => make().DeleteData("t", "c"));

            Assert.Equal("DeleteData needs at least one value to match. (Parameter 'values')", error.Message);
        }
    }

    [Fact(DisplayName = "the entity overload refuses a call without rows like the column overload, and keeps "
        + "recording what it recorded before for a row")]
    public void The_entity_overload_refuses_no_rows()
    {
        foreach (var make in Builders)
        {
            var builder = Busy(make);

            var error = Assert.Throws<ArgumentException>(() => builder.InsertData<object>(Probe, v => [v]));

            Assert.Equal("rows", error.ParamName);
            Assert.Single(builder.Nodes);
            Assert.Equal(
                "INSERT INTO \"probes\" (\"label\") VALUES ('a')",
                Assert.Single(make().InsertData<object>(Probe, v => [v], "a").Operations));
        }
    }

    [Fact(DisplayName = "a valid InsertData and DeleteData still record exactly one operation each")]
    public void A_valid_call_still_records_one_operation()
    {
        foreach (var make in Builders)
        {
            var builder = make().InsertData("t", ["g"], ["a"]).DeleteData("t", "g", "a");

            Assert.Equal(2, builder.Nodes.Count);
        }
    }
}
