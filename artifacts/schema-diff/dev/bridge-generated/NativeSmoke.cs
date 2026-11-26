using GeneratedBridge117;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace BridgeNative;

internal static class NativeSmoke
{
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length != 4) throw new InvalidOperationException("provider, create flag, original and fresh required");
        var provider = args[0]; var created = bool.Parse(args[1]);
        var artifact = Factory(provider, created);
        if (provider == "postgres")
        {
            var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
                ?? throw new IOException("owned fixture required");
            await using var source = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(server)
                { Database = args[2] }.ConnectionString);
            await using var freshSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(server)
                { Database = args[3] }.ConnectionString);
            await VerifyAsync(new(source, PostgresProvider.Instance), new(freshSource, PostgresProvider.Instance),
                artifact, created);
        }
        else
        {
            SqliteProviderFactory.Register();
            await using var original = await OrmLite.OpenAsync(new OrmLiteOptions
                { Provider = "sqlite", Path = args[2], Pooling = false });
            await using var fresh = await OrmLite.OpenAsync(new OrmLiteOptions
                { Provider = "sqlite", Path = args[3], Pooling = false });
            await VerifyAsync(original, fresh, artifact, created);
        }
        Console.WriteLine("GENERATED_NATIVE_BRIDGE_PASS runtime=" + Environment.Version
            + " provider=" + provider + " create=" + created + " version=" + artifact.Version);
    }

    private static SchemaDiffArtifact Factory(string provider, bool created) => (provider, created) switch
    {
        ("postgres", false) => PostgresAddBridgeFactory.Create(),
        ("postgres", true) => PostgresCreateBridgeFactory.Create(),
        ("sqlite", false) => SqliteAddBridgeFactory.Create(),
        ("sqlite", true) => SqliteCreateBridgeFactory.Create(),
        _ => throw new InvalidOperationException("unsupported fixture"),
    };

    private static async Task VerifyAsync(OrmContext original, OrmContext fresh,
        SchemaDiffArtifact artifact, bool created)
    {
        var schema = Schema(created);
        var review = new SchemaDiffReview(artifact.ReviewDigest, "fixture117");
        var lineage = new SchemaDiffLineage(schema, Array.Empty<string>());
        var upgrade = new SchemaDiffFixtureRequest(artifact, SchemaDiffFixtureMode.UpgradeObserved, review, lineage);
        var repeat = await SchemaDiffFixtureRunner.RunAsync(original, upgrade);
        Require(repeat.Outcome == SchemaDiffApplyOutcome.AlreadyApplied && repeat.Units.Count == 0);
        var down = await SchemaDiffFixtureRunner.RunAsync(original,
            upgrade with { Mode = SchemaDiffFixtureMode.RevertAppliedUpgrade });
        Require(down.Outcome == SchemaDiffApplyOutcome.Applied 
            && down.Units.Single().Outcome == SchemaUnitOutcome.Committed);
        var up = await SchemaDiffFixtureRunner.RunAsync(original, upgrade);
        Require(up.Outcome == SchemaDiffApplyOutcome.Applied 
            && up.Units.Single().Outcome == SchemaUnitOutcome.Committed);
        var covered = await SchemaDiffFixtureRunner.RunAsync(fresh,
            upgrade with { Mode = SchemaDiffFixtureMode.FreshCurrentBaseline });
        Require(covered.Outcome == SchemaDiffApplyOutcome.CoveredByCurrentBaseline && covered.Units.Count > 0);
        Require(covered.KnownCommittedVersions.Contains(artifact.CoverageVersion)
            && !covered.KnownCommittedVersions.Contains(artifact.Version));
        var coveredRepeat = await SchemaDiffFixtureRunner.RunAsync(fresh,
            upgrade with { Mode = SchemaDiffFixtureMode.FreshCurrentBaseline });
        Require(coveredRepeat.Outcome == SchemaDiffApplyOutcome.CoveredByCurrentBaseline 
            && coveredRepeat.Units.Count == 0);
        await RefuseCoveredDownAsync(fresh, upgrade);
    }

    private static OrmSchema Schema(bool created)
    {
        var table = created ? "bridge created" : "bridge items";
        var entity = EntityDescriptor.Of("NativeBridge", table, true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Title", "title", ColumnType.Text, true, false)
            .Property("Added", "added", ColumnType.Text, true, false);
        return OrmSchema.Of(created ? "bridge-create" : "bridge-add", entity);
    }

    private static async Task RefuseCoveredDownAsync(OrmContext fresh, SchemaDiffFixtureRequest request)
    {
        try
        {
            await SchemaDiffFixtureRunner.RunAsync(fresh,
                request with { Mode = SchemaDiffFixtureMode.RevertAppliedUpgrade });
        }
        catch (SchemaDiffException error) when (error.Error == SchemaDiffError.BaselineOwnsObjects) { return; }
        throw new InvalidOperationException("baseline ownership refusal missing");
    }

    private static void Require(bool value)
    {
        if (!value) throw new InvalidOperationException("native guarded bridge contract");
    }
}
