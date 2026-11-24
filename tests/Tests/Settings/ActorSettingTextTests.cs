using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Settings;

[Collection(ActorSettingCollection.Name)]
public sealed class ActorSettingTextTests
{
    private const string Default = "ormlite.actor";

    private const string Probe = "probe.actor";

    private const string StampDefault = "SELECT set_config('ormlite.actor', @v, false)";

    private const string RefusalDefault =
        "GatedDataSource refuses the request data source: its connection string sets No Reset On Close, "
        + "which would let a pooled connection keep the previous caller's ormlite.actor. Remove No Reset On Close from the connection string.";

    private const string TriggerLineDefault = "  v_actor := current_setting('ormlite.actor', true);";

    private static readonly RowHistoryTable Docs = new("docs", "id", "deleted_at", "scope_id", []);

    private sealed class StubSource(string connectionString) : DbDataSource
    {
        public override string ConnectionString { get; } = connectionString;

        protected override DbConnection CreateDbConnection() => throw new NotSupportedException();
    }

    private static string Refusal()
    {
        var stub = new StubSource("Host=h;No Reset On Close=true");
        return Assert.Throws<InvalidOperationException>(() => new GatedDataSource(PostgresProvider.Instance, stub, stub, stub)).Message;
    }

    [Fact(DisplayName = "the library default actor setting is ormlite.actor")]
    public void The_default_setting_is_the_neutral_literal()
    {
        Assert.Equal(Default, OrmLiteSettings.DefaultActorSetting);
    }

    [Fact(DisplayName = "under the library default the stamp statement, the gate refusal and the trigger text carry ormlite.actor verbatim")]
    public void Under_the_library_default_the_three_texts_are_exact()
    {
        using var scope = ActorSettingScope.Fresh();

        Assert.Equal(StampDefault, PostgresActorStamp.Statement);
        Assert.Equal(RefusalDefault, Refusal());
        Assert.Contains(TriggerLineDefault, RowHistoryDdl.FunctionSqlFor(PostgresProvider.Instance, Default).Split('\n'));
        Assert.Equal(
            RowHistoryDdl.ReconcileFor(PostgresProvider.Instance, Default, [Docs]),
            RowHistoryDdl.Reconcile(PostgresProvider.Instance, [Docs]));
    }

    [Fact(DisplayName = "under a registered name the stamp statement and the gate refusal change only at the setting name")]
    public void Under_a_registered_name_the_stamp_and_the_refusal_change_only_at_the_name()
    {
        using var scope = ActorSettingScope.Fresh();
        OrmLiteSettings.UseActorSetting(Probe);

        Assert.Equal(StampDefault.Replace(Default, Probe), PostgresActorStamp.Statement);
        Assert.Equal(RefusalDefault.Replace(Default, Probe), Refusal());
    }

    [Fact(DisplayName = "the trigger function text carries the setting name exactly once and the reconcile outputs differ only there")]
    public void The_function_text_carries_the_name_once_and_the_outputs_differ_only_there()
    {
        var baseline = RowHistoryDdl.ReconcileFor(PostgresProvider.Instance, Default, [Docs]);
        var probe = RowHistoryDdl.ReconcileFor(PostgresProvider.Instance, Probe, [Docs]);

        var function = RowHistoryDdl.FunctionSqlFor(PostgresProvider.Instance, Probe);
        Assert.Equal(1, function.Split(Probe).Length - 1);
        Assert.DoesNotContain(Default, function, StringComparison.Ordinal);
        Assert.Equal(baseline.Length, probe.Length);
        var differing = Enumerable.Range(0, baseline.Length).Where(i => baseline[i] != probe[i]).ToArray();
        var changed = Assert.Single(differing);
        Assert.StartsWith("CREATE OR REPLACE FUNCTION", baseline[changed], StringComparison.Ordinal);
        Assert.Equal(baseline[changed].Replace(Default, Probe), probe[changed]);
        Assert.NotEqual(RowHistoryDdl.StatementsHash(baseline), RowHistoryDdl.StatementsHash(probe));
    }

    [Fact(DisplayName = "the trigger function text refuses a setting name that is not a lowercase dotted identifier")]
    public void The_function_text_refuses_an_invalid_name()
    {
        Assert.Throws<ArgumentException>(() => RowHistoryDdl.FunctionSqlFor(PostgresProvider.Instance, "x'); DROP TABLE t; --"));
        Assert.Throws<ArgumentException>(() => RowHistoryDdl.ReconcileFor(PostgresProvider.Instance, "NoDot", []));
    }
}
