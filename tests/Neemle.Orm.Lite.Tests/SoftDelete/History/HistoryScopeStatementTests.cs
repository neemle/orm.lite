using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.History;

// No server: the history, revert, restore and trash statements are compiled for each scope shape and the compiled
// parameters say whether the tenant reached the statement (HistoryScope is the one rule they all ask).
public sealed class HistoryScopeStatementTests
{
    private static readonly Guid Tenant = Guid.Parse("c0000000-0000-0000-0000-00000000000c");

    private static readonly Guid Row = Guid.Parse("a0000000-0000-0000-0000-00000000000a");

    private static ISqlProvider Pg => PostgresProvider.Instance;

    private static ISqlProvider ProviderOf(string id) =>
        id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    private static ISoftEntry Shape(string shape)
    {
        HistoryWorld.Register();
        var task = HistoryWorld.Entry<HistTask>();
        return shape switch
        {
            "platform" => new HistoryShapeEntry(task, true, null),
            "tenant" => new HistoryShapeEntry(task, false, "tenant_id"),
            "tenant-root" => new HistoryShapeEntry(task, false, "id"),
            _ => new HistoryShapeEntry(task, false, null),
        };
    }

    private static bool BindsTenant(ISqlProvider provider, Node tree, StatementKind kind) =>
        HistoryStatements.Compile(provider, tree, kind).Parameters.Any(p => Equals(p.Value, Tenant));

    private static (string Verb, Func<Node> Build)[] AllBuilders(ISoftEntry entry) =>
    [
        ("RevertAsync", () => HistoryStatements.Revert(entry, true, entry.Table, Row, 7, Tenant)),
        ("LockAsync", () => HistoryStatements.LockRow(entry, Row, Tenant)),
        ("NewestVersionAsync", () => HistoryStatements.NewestVersion(entry, Row, Tenant)),
        ("VersionExistsAsync", () => HistoryStatements.VersionExists(entry, Row, 7, Tenant)),
        ("PlanAsync", () => HistoryStatements.LatestVersion(entry, Row, Tenant)),
        ("IsDeletedUnderShareLockAsync", () => HistoryStatements.ShareLock(entry, Row, Tenant)),
        ("PlanAsync", () => HistoryStatements.LockDeleted(entry, ["id"], [Row], Tenant)),
    ];

    [Theory(DisplayName = "a tenant entry and a tenant-root entry carry the tenant in the revert and in the three "
        + "history reads of the revert and the restore, and a Platform entry carries none")]
    [InlineData("postgres", "tenant", true)]
    [InlineData("postgres", "tenant-root", true)]
    [InlineData("postgres", "platform", false)]
    [InlineData("sqlite", "tenant", true)]
    [InlineData("sqlite", "tenant-root", true)]
    [InlineData("sqlite", "platform", false)]
    public void The_tenant_is_bound_exactly_when_the_entry_is_scoped(string providerId, string shape, bool scoped)
    {
        var provider = ProviderOf(providerId);
        var entry = Shape(shape);
        var statements = new (string, Node, StatementKind)[]
        {
            ("Revert", HistoryStatements.Revert(entry, true, entry.Table, Row, 7, Tenant), StatementKind.Write),
            ("NewestVersion", HistoryStatements.NewestVersion(entry, Row, Tenant), StatementKind.Read),
            ("VersionExists", HistoryStatements.VersionExists(entry, Row, 7, Tenant), StatementKind.Read),
            ("LatestVersion", HistoryStatements.LatestVersion(entry, Row, Tenant), StatementKind.Read),
        };

        foreach (var (name, tree, kind) in statements)
            Assert.True(BindsTenant(provider, tree, kind) == scoped, $"{name} on {shape} ({providerId})");
    }

    [Theory(DisplayName = "the row locks of the revert and the restore carry the tenant for a tenant and a tenant-root "
        + "entry and none for a Platform entry")]
    [InlineData("tenant", true)]
    [InlineData("tenant-root", true)]
    [InlineData("platform", false)]
    public void The_row_locks_bind_the_tenant_exactly_when_the_entry_is_scoped(string shape, bool scoped)
    {
        var entry = Shape(shape);
        var locks = new (string, Node)[]
        {
            ("LockRow", HistoryStatements.LockRow(entry, Row, Tenant)),
            ("ShareLock", HistoryStatements.ShareLock(entry, Row, Tenant)),
            ("LockDeleted", HistoryStatements.LockDeleted(entry, ["id"], [Row], Tenant)),
        };

        foreach (var (name, tree) in locks)
            Assert.True(BindsTenant(Pg, tree, StatementKind.Read) == scoped, $"{name} on {shape}");
    }

    [Fact(DisplayName = "an entry that is not Platform and names no tenant column is refused by every statement of "
        + "the history, revert and restore, naming the call, before anything is compiled")]
    public void An_entry_without_a_tenant_column_is_refused_by_every_statement()
    {
        var entry = Shape("none");

        foreach (var (verb, build) in AllBuilders(entry))
        {
            var refusal = Assert.Throws<InvalidOperationException>(build);

            Assert.Contains(verb, refusal.Message, StringComparison.Ordinal);
            Assert.Contains("requires its tenant column and a tenant id", refusal.Message, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "the trash lists a tenant-root entry under its tenant and only when platform rows are asked "
        + "for, and lists nothing for an entry with no tenant column")]
    public void The_trash_scopes_a_tenant_root_entry_and_skips_an_entry_without_a_tenant_column()
    {
        var withPlatform = new SoftTrashFilter(IncludePlatform: true);
        var root = HistoryStatements.TrashUnion([Shape("tenant-root")], withPlatform, Tenant, 10, 0);

        Assert.NotNull(root);
        Assert.True(BindsTenant(Pg, root, StatementKind.Read));
        Assert.Null(HistoryStatements.TrashUnion([Shape("tenant-root")], new SoftTrashFilter(), Tenant, 10, 0));
        Assert.Null(HistoryStatements.TrashUnion([Shape("none")], withPlatform, Tenant, 10, 0));
    }

    [Theory(DisplayName = "the history reads refuse a call that gives no tenant for a tenant entry before they touch "
        + "the data source")]
    [InlineData("versions")]
    [InlineData("state")]
    [InlineData("exists")]
    public async Task A_read_without_a_tenant_is_refused_for_a_tenant_entry(string read)
    {
        HistoryWorld.Register();
        Func<Task> call = read switch
        {
            "versions" => () => RowHistoryRead.VersionsAsync(null!, HistTask.Table, Row),
            "state" => () => RowHistoryRead.StateOfVersionAsync(null!, HistTask.Table, Row, 7),
            _ => () => RowHistoryRead.RowExistsAsync(null!, HistTask.Table, Row),
        };

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(call);

        Assert.Contains("requires its tenant column and a tenant id", refusal.Message, StringComparison.Ordinal);
    }
}
