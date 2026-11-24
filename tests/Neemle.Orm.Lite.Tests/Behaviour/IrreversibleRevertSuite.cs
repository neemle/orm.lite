using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public abstract class IrreversibleRevertSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Ctx => fixture.Context;

    private static CreateTable Table(string name) =>
        new(name, NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)]));

    private static AddColumn Note(string table) => new(table, new ColumnDef("note", new TypeRef(ColumnType.Text)));

    private Task RunAsync(MigrationContribution contribution) =>
        MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, [contribution]);

    private Task RevertAsync(MigrationContribution contribution) =>
        MigrationRunner.RevertAsync(Ctx.DataSource, Ctx.Provider, contribution);

    private Task<InvalidOperationException> RefusedAsync(MigrationContribution contribution) =>
        Assert.ThrowsAsync<InvalidOperationException>(() => RevertAsync(contribution));

    private Task SeedHistoryAsync(string version, string table) =>
        RunAsync(MigrationContribution.Of(version, Reversibility.Reversible, [Table(table)]));

    private async Task<string> RowsAsync(string statement)
    {
        var rows = await fixture.RowsAsync(statement);
        var cells = rows.Select(r => r.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)));
        return string.Join(" ; ", cells.Select(row => string.Join("|", row)));
    }

    private async Task<string> ShapeAsync(string table)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        var catalog = Ctx.Provider.CreateCatalog(connection);
        if (!await catalog.TableExistsAsync(table)) return "absent";
        return string.Join(",", (await catalog.ColumnsAsync(table)).Select(c => c.Name));
    }

    private Task<string> HistoryAsync(string version) =>
        RowsAsync($"SELECT count(*) FROM \"schema_migrations\" WHERE \"version\" = '{version}'");

    private static void AssertRefusal(Exception refusal, string version)
    {
        Assert.Contains(version, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("Irreversible", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was executed", refusal.Message, StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "an applied Irreversible migration of invertible operations is refused by name "
        + "and its table, column, row and history row stay as they were")]
    public async Task A_refused_revert_leaves_everything_in_place()
    {
        fixture.RequireAvailable();
        var contribution = MigrationContribution.Of(
            "1500_irr_applied", Reversibility.Irreversible, [Table("mz_irr_a"), Note("mz_irr_a")]);
        await RunAsync(contribution);
        await fixture.ExecuteAsync("INSERT INTO \"mz_irr_a\" (\"id\", \"note\") VALUES ('k', 'kept')");
        var shape = await ShapeAsync("mz_irr_a");
        var rows = await RowsAsync("SELECT \"id\", \"note\" FROM \"mz_irr_a\"");

        var refusal = await RefusedAsync(contribution);

        AssertRefusal(refusal, "1500_irr_applied");
        Assert.Equal("id,note", shape);
        Assert.Equal(shape, await ShapeAsync("mz_irr_a"));
        Assert.Equal("k|kept", rows);
        Assert.Equal(rows, await RowsAsync("SELECT \"id\", \"note\" FROM \"mz_irr_a\""));
        Assert.Equal("1", await HistoryAsync("1500_irr_applied"));
    }

    [SkippableFact(DisplayName = "an Irreversible migration that was never applied is refused as Irreversible, "
        + "not as not applied, and nothing is created")]
    public async Task A_never_applied_irreversible_migration_is_refused_as_irreversible()
    {
        fixture.RequireAvailable();
        await SeedHistoryAsync("1501_irr_seed", "mz_irr_seed");
        var contribution = MigrationContribution.Of(
            "1501_irr_never", Reversibility.Irreversible, [Table("mz_irr_never"), Note("mz_irr_never")]);

        var refusal = await RefusedAsync(contribution);

        AssertRefusal(refusal, "1501_irr_never");
        Assert.DoesNotContain("is not applied", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("absent", await ShapeAsync("mz_irr_never"));
    }

    [SkippableFact(DisplayName = "an applied Irreversible migration that carries a Down is refused, "
        + "the Down is never invoked and its marker table never appears")]
    public async Task A_down_carried_by_an_irreversible_migration_does_not_run()
    {
        fixture.RequireAvailable();
        var invoked = 0;
        var contribution = MigrationContribution.Of(
            "1502_irr_with_down",
            Reversibility.Irreversible,
            [Table("mz_irr_d"), Note("mz_irr_d")],
            b => { invoked++; b.Add(Table("mz_irr_marker")); });
        await RunAsync(contribution);

        var refusal = await RefusedAsync(contribution);

        AssertRefusal(refusal, "1502_irr_with_down");
        Assert.Equal(0, invoked);
        Assert.Equal("absent", await ShapeAsync("mz_irr_marker"));
        Assert.Equal("id,note", await ShapeAsync("mz_irr_d"));
        Assert.Equal("1", await HistoryAsync("1502_irr_with_down"));
    }

    [SkippableFact(DisplayName = "a Reversible migration with an explicit Down still runs that Down "
        + "and forgets its version")]
    public async Task A_reversible_migration_with_a_down_still_reverts()
    {
        fixture.RequireAvailable();
        var contribution = MigrationContribution.Of(
            "1503_rev_with_down", Reversibility.Reversible, [Table("mz_irr_r")], b => b.DropTable("mz_irr_r"));
        await RunAsync(contribution);
        Assert.Equal("id", await ShapeAsync("mz_irr_r"));

        await RevertAsync(contribution);

        Assert.Equal("absent", await ShapeAsync("mz_irr_r"));
        Assert.Equal("0", await HistoryAsync("1503_rev_with_down"));
    }

    [SkippableFact(DisplayName = "a Reversible migration whose Up cannot be inverted and that has no Down "
        + "still says none can be derived, and nothing is dropped")]
    public async Task A_reversible_migration_without_a_derivable_down_still_says_so()
    {
        fixture.RequireAvailable();
        await SeedHistoryAsync("1504_rev_seed", "mz_irr_lossy");
        var contribution = MigrationContribution.Of(
            "1504_rev_lossy", Reversibility.Reversible, [new DropColumn("mz_irr_lossy", "id")]);

        var refusal = await RefusedAsync(contribution);

        Assert.Contains("1504_rev_lossy", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("none can be derived", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Irreversible", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("id", await ShapeAsync("mz_irr_lossy"));
    }

    [SkippableFact(DisplayName = "a Reversible migration that was never applied still says there is nothing to revert")]
    public async Task A_reversible_migration_never_applied_still_says_so()
    {
        fixture.RequireAvailable();
        await SeedHistoryAsync("1505_rev_seed", "mz_irr_seed2");
        var contribution = MigrationContribution.Of(
            "1505_rev_never", Reversibility.Reversible, [Table("mz_irr_never2")]);

        var refusal = await RefusedAsync(contribution);

        Assert.Contains(
            "migration 1505_rev_never is not applied, so there is nothing to revert.",
            refusal.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Irreversible", refusal.Message, StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "a contribution with neither a Down nor operations still says it has no Down")]
    public async Task A_contribution_without_down_or_nodes_still_says_so()
    {
        fixture.RequireAvailable();
        var contribution = new MigrationContribution("1506_rev_bare", Reversibility.Reversible, _ => [], null);

        var refusal = await RefusedAsync(contribution);

        Assert.Contains("migration 1506_rev_bare has no Down.", refusal.Message, StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "an applied migration whose Reversibility is not a named member is refused by name "
        + "and its table, row and history row stay as they were")]
    public async Task An_undefined_declaration_is_not_reverted()
    {
        fixture.RequireAvailable();
        var applied = MigrationContribution.Of(
            "1507_rev_undefined", Reversibility.Reversible, [Table("mz_irr_u"), Note("mz_irr_u")]);
        await RunAsync(applied);
        await fixture.ExecuteAsync("INSERT INTO \"mz_irr_u\" (\"id\", \"note\") VALUES ('k', 'kept')");

        var refusal = await RefusedAsync(applied with { Reversibility = (Reversibility)0 });

        Assert.Contains("1507_rev_undefined", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("not a named member", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("id,note", await ShapeAsync("mz_irr_u"));
        Assert.Equal("k|kept", await RowsAsync("SELECT \"id\", \"note\" FROM \"mz_irr_u\""));
        Assert.Equal("1", await HistoryAsync("1507_rev_undefined"));
    }
}
