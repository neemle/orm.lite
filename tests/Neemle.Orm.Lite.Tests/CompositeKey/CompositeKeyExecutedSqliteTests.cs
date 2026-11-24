using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Sqlite;
using Xunit;
using static Neemle.Orm.Lite.Tests.CompositeKey.CompositeKeySources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.CompositeKey;

public sealed class CompositeKeyExecutedSqliteTests : IAsyncLifetime
{
    private static readonly Guid OrderA = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static readonly Guid OrderB = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static readonly Guid Apple = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    private static readonly Guid Bread = Guid.Parse("00000000-0000-0000-0000-0000000000c2");

    private static readonly Guid Cheese = Guid.Parse("00000000-0000-0000-0000-0000000000c3");

    private static readonly ImmutableArray<Diagnostic> Reported;

    private static readonly GeneratedModel Model;

    private string _directory = "";

    private OrmContext _db = null!;

    static CompositeKeyExecutedSqliteTests()
    {
        var (diagnostics, _, output) = Run(LineWithQueries);
        Reported = diagnostics;
        Model = GeneratedModel.Of(output);
    }

    public async Task InitializeAsync()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ormlite-composite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        SqliteProviderFactory.Register();
        _db = await OrmLite.OpenAsync(
            new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(_directory, "composite.db") });
        await using var create = _db.DataSource.CreateCommand(
            "create table lines (order_id text not null, product_id text not null, qty integer not null, "
            + "primary key (order_id, product_id))");
        await create.ExecuteNonQueryAsync();
        Model.RunModuleInitializer();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static MethodInfo Helper(string name) =>
        Model.Class("LineOrm").Assembly.GetType("Fixture.LineQueries", throwOnError: true)!.GetMethod(name)!;

    private Task Add(Guid order, Guid product, int qty) =>
        (Task)Helper("Add").Invoke(null, [_db, order, product, qty])!;

    private Task<int> DeleteOne(Guid order, Guid product) =>
        (Task<int>)Helper("DeleteOne").Invoke(null, [_db, order, product])!;

    private async Task<long> CountAsync(Guid order)
    {
        await using var command = _db.DataSource.CreateCommand(
            "select count(*) from lines where order_id = '" + order.ToString("D") + "'");
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task SeedAsync()
    {
        await Add(OrderA, Apple, 1);
        await Add(OrderA, Bread, 2);
        await Add(OrderA, Cheese, 3);
        await Add(OrderB, Apple, 4);
    }

    [Fact(
        DisplayName = "AC6 [SEC] a composite-key entity is refused at generation and its repository has no get, "
        + "update or delete by key")]
    public void The_composite_entity_is_refused_and_has_no_member_that_takes_a_key()
    {
        var refusal = Assert.Single(Reported, d => d.Id == "ORMLITE022");
        Assert.Contains("'Line'", refusal.GetMessage(), StringComparison.Ordinal);
        var names = Model.Class("LineOrm")
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();
        Assert.DoesNotContain("GetAsync", names);
        Assert.DoesNotContain("UpdateAsync", names);
        Assert.DoesNotContain("DeleteAsync", names);
    }

    [Fact(
        DisplayName = "AC6 the supported way to delete one row of a composite table, a query on every key "
        + "column, removes exactly that row on SQLite")]
    public async Task A_delete_on_every_key_column_removes_exactly_one_row()
    {
        await SeedAsync();

        Assert.Equal(1, await DeleteOne(OrderA, Bread));

        Assert.Equal(2, await CountAsync(OrderA));
        Assert.Equal(1, await CountAsync(OrderB));
    }

    [Fact(DisplayName = "AC6 a delete on every key column that matches no row removes nothing")]
    public async Task A_delete_that_matches_no_row_removes_nothing()
    {
        await SeedAsync();

        Assert.Equal(0, await DeleteOne(OrderB, Bread));

        Assert.Equal(3, await CountAsync(OrderA));
        Assert.Equal(1, await CountAsync(OrderB));
    }
}
