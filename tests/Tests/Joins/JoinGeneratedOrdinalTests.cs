using System.Data;
using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

[Collection("Graph")]
public sealed class JoinGeneratedOrdinalTests
{
    [Fact(DisplayName = "[UC-008][AC-114-07] existing generated bootstrap registers managed and unmanaged codecs")]
    public void Compiled_bootstrap_registers_ten_columns()
    {
        Assert.Equal(8, JoinCompiledConsumer.Columns("Probe").Count);
        Assert.Equal(2, JoinCompiledConsumer.Columns("External").Count);
    }

    [Theory(DisplayName = "[UC-008][AC-114-07] generated readers use supplied ordinal rather than entity column name")]
    [InlineData("Probe", "Id")] [InlineData("Probe", "Name")] [InlineData("Probe", "Note")]
    [InlineData("Probe", "Enabled")] [InlineData("Probe", "Rank")] [InlineData("Probe", "Count")]
    [InlineData("Probe", "Edited")] [InlineData("Probe", "Lower")]
    [InlineData("External", "Id")] [InlineData("External", "Label")]
    public void Generated_codec_reads_new_nonzero_ordinal_values(string entity, string property)
    {
        var binding = JoinCompiledConsumer.Columns(entity)[property];
        var expected = Value(property);
        using var table = Table(expected.GetType(), expected);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var value = binding.Reader.DynamicInvoke(reader, 1);
        Assert.Equal(expected, value);
        Assert.IsType(expected.GetType(), value);
        if (value is DateTime date) Assert.Equal(DateTimeKind.Utc, date.Kind);
    }

    [Theory(DisplayName = "[UC-008][AC-114-07] nullable generated codecs return null without typed value decoding")]
    [InlineData("Probe", "Note")] [InlineData("Probe", "Edited")] [InlineData("External", "Label")]
    public void Nullable_codec_reads_new_dbnull_input(string entity, string property)
    {
        var binding = JoinCompiledConsumer.Columns(entity)[property];
        var type = System.Nullable.GetUnderlyingType(binding.ValueType) ?? binding.ValueType;
        using var table = Table(type, DBNull.Value);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        Assert.Null(binding.Reader.DynamicInvoke(reader, 1));
    }

    private static DataTable Table(Type type, object value)
    {
        var table = new DataTable();
        table.Columns.Add("unrelated_zero", typeof(string));
        table.Columns.Add("unrelated_one", type);
        if (type == typeof(DateTime)) table.Columns[1].DateTimeMode = DataSetDateTime.Utc;
        table.Rows.Add("not the requested value", value);
        return table;
    }

    private static object Value(string property) => property switch
    {
        "Id" => Guid.Parse("00000001-0000-0000-0000-000000000000"),
        "Enabled" => true, "Rank" => 42, "Count" => 9000000000L,
        "Edited" => new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc),
        "Lower" => "alpha", _ => "row alpha",
    };
}
