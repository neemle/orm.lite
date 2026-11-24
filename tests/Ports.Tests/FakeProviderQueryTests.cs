using System.Data;
using System.Data.Common;
using Xunit;

namespace Neemle.OrmLite.Ports.Tests;

public sealed class FakeProviderQueryTests
{
    private static readonly Guid Alpha = Guid.NewGuid();

    private static readonly Guid Bravo = Guid.NewGuid();

    private readonly PortsScenario _scenario = new();

    public FakeProviderQueryTests()
    {
        OrmMeta.Register<PortsWidget>(PortsScenario.Table, Column, ["id", "name", "rank"]);
        _scenario.Database.Serve(PortsScenario.Table, Widgets());
    }

    private static string Column(string member) => member switch
    {
        nameof(PortsWidget.Id) => "id",
        nameof(PortsWidget.Name) => "name",
        nameof(PortsWidget.Rank) => "rank",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "unknown member"),
    };

    private static PortsWidget Map(DbDataReader reader) =>
        new(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2));

    private static DataTable Widgets()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        table.Columns.Add("name", typeof(string));
        table.Columns.Add("rank", typeof(int));
        table.Rows.Add(Alpha, "alpha", 20);
        table.Rows.Add(Bravo, "bravo", 30);
        return table;
    }

    [Fact(DisplayName = "a query over the fake provider is rendered by its serializer, bound with its parameter "
        + "prefix, and read back through the ordinary ADO.NET reader")]
    public async Task A_read_runs_through_the_fake_serializer_and_connection()
    {
        await using var connection = await _scenario.Source.OpenConnectionAsync();
        var query = new Query<PortsWidget>(_scenario.NewContext(), Map).On(connection);

        var rows = await query.Where(w => w.Rank > 10).ToListAsync();

        Assert.Equal(new[] { Alpha, Bravo }, rows.Select(r => r.Id).ToList());
        var statement = Assert.Single(_scenario.Database.Statements);
        Assert.StartsWith("SELECT", statement.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"" + PortsScenario.Table + "\"", statement.Text, StringComparison.Ordinal);
        var bound = Assert.Single(statement.Values);
        Assert.Contains(":" + bound.Key, statement.Text, StringComparison.Ordinal);
        Assert.Equal(10, Convert.ToInt32(bound.Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "a filtered delete over the fake provider is one write statement and reports the rows "
        + "the server affected")]
    public async Task A_delete_runs_as_one_write_and_reports_the_affected_rows()
    {
        _scenario.Database.Affected = 3;
        await using var connection = await _scenario.Source.OpenConnectionAsync();
        var query = new Query<PortsWidget>(_scenario.NewContext(), Map).On(connection);

        var affected = await query.Where(w => w.Name == "alpha").DeleteAsync();

        Assert.Equal(3, affected);
        var statement = Assert.Single(_scenario.Database.Statements);
        Assert.StartsWith("DELETE", statement.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"" + PortsScenario.Table + "\"", statement.Text, StringComparison.Ordinal);
    }
}
