using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.MigrationReversibility;

public sealed class UndefinedDeclarationRefusalTests
{
    private const string Version = "1600_undefined";

    private static ISqlProvider ProviderOf(string id) =>
        id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    private static readonly Node[] Invertible =
        [new CreateTable("ud_t", NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)]))];

    private static MigrationContribution Hand(Reversibility declared) =>
        new(Version, declared, _ => ["SELECT 1"], null) { Nodes = _ => Invertible };

    private static Task RevertAsync(string providerId, MigrationContribution contribution) =>
        MigrationRunner.RevertAsync(new NeverUsedDataSource(), ProviderOf(providerId), contribution);

    private static Task RunAsync(string providerId, MigrationContribution contribution) =>
        MigrationRunner.RunAsync(new NeverUsedDataSource(), ProviderOf(providerId), [contribution]);

    [Theory(DisplayName = "reverting an invertible hand-built entry whose Reversibility is not a named member is "
        + "refused by name before the data source is touched, so no derived Down can drop its table")]
    [InlineData("postgres", 0)]
    [InlineData("sqlite", 0)]
    [InlineData("postgres", 7)]
    [InlineData("sqlite", -1)]
    public async Task An_undefined_value_is_not_reverted(string providerId, int value)
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RevertAsync(providerId, Hand((Reversibility)value)));

        Assert.Contains(Version, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("not a named member", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing was executed", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "running an entry whose Reversibility is not a named member is refused by name "
        + "before the data source is touched")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task An_undefined_value_is_not_run(string providerId)
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(providerId, Hand(default)));

        Assert.Contains(Version, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("not a named member", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "default(MigrationContribution) is refused by RunAsync and RevertAsync as a contribution "
        + "without a Version, never as a NullReferenceException")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task A_default_contribution_is_refused(string providerId)
    {
        var run = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(providerId, default));
        var revert = await Assert.ThrowsAsync<InvalidOperationException>(() => RevertAsync(providerId, default));

        Assert.Contains("no Version", run.Message, StringComparison.Ordinal);
        Assert.Contains("no Version", revert.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an entry with a Version and no Render is refused by name")]
    public async Task A_contribution_without_render_is_refused()
    {
        var bare = new MigrationContribution(Version, Reversibility.Reversible, null!, null);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync("sqlite", bare));

        Assert.Contains(Version + " has no Render", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Of and For refuse an undefined Reversibility at construction")]
    public void Construction_refuses_an_undefined_value()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MigrationContribution.Of(Version, default, Invertible));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MigrationContribution.For<Probe>(Version, (Reversibility)9, false));
        var named = MigrationContribution.Of(Version, Reversibility.Reversible, Invertible);
        Assert.Equal(Reversibility.Reversible, named.Reversibility);
    }

    private sealed class Probe : Migration
    {
        public override void Up(MigrationBuilder builder) =>
            builder.CreateTable("ud_p", t => t.Column("id", ColumnType.Text));
    }
}
