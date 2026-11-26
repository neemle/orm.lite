using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace FoundationProof;

internal static class SessionProof
{
    internal static async Task RunAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new IOException("owned fixture required");
        await using var source = NpgsqlDataSource.Create(server);
        await using var postgres = new OrmContext(source, PostgresProvider.Instance);
        await VerifyAsync(postgres, "public", null);
        var path = Path.Combine(Path.GetTempPath(), "orml117-session-" + Guid.NewGuid().ToString("N") + ".db");
        SqliteProviderFactory.Register();
        await using var sqlite = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = path });
        await VerifyAsync(sqlite, "main", true);
        await VerifyAsync(sqlite, "main", false);
        Console.WriteLine("SESSION_PROOF_PASS runtime=" + Environment.Version);
    }

    private static async Task VerifyAsync(OrmContext context, string ns, bool? original)
    {
        var target = Target(ns);
        var observed = await SchemaDiff.ObserveAsync(context, target);
        Check.That(SchemaDiff.Compare(target, observed).Status == SchemaDiffStatus.Changes, "public metadata observe");
        var artifact = SchemaDiff.Emit(SchemaDiff.Compare(target, observed), new("0220_guard",
             "Generated117", "Guard"));
        using var actor = DbActor.EnterSystem();
        await using var raw = await context.DataSource.OpenConnectionAsync();
        await context.Provider.StampActorAsync(raw, DbActorValue.System, default);
        var budget = new SchemaStatementBudget(target.Limits.MaxStatements);
        var connection = new SchemaBudgetConnection(raw, budget);
        if (original is { } setting) await SetForeignKeysAsync(connection, setting);
        var adapter = (ISchemaDiffProvider)context.Provider;
        var request = new SchemaDiffInspection(target, artifact.Recipe.Before.ProviderProfile);
        await using var session = await adapter.BeginDiffApplyAsync(connection, request, default);
        if (original is { } prep) await SetForeignKeysAsync(connection, !prep);
        await TransactionAsync(context, raw, connection, session, artifact);
        budget.Cleanup = true;
        var restoration = await session.RestoreAndVerifyAsync(default);
        Check.That(restoration == (original is null ? SchemaRestoration.NotRequired : SchemaRestoration.Restored),
            "provider-specific verified restoration");
        if (original is { } expected) await CheckForeignKeysAsync(connection, expected);
        Check.That(budget.Statements > 0, "budget sees native catalog statements");
        Check.That(original is null ? budget.CleanupStatements == 0 : budget.CleanupStatements >= 2,
            "PG has no restore SQL; SQLite restore reads are counted");
        Console.WriteLine("SESSION_PROVIDER_PASS " + context.Provider.Id + " original=" + original
            + " statements=" + budget.Statements + " cleanup=" + budget.CleanupStatements);
    }

    private static SchemaDiffTarget Target(string ns)
    {
        var entity = EntityDescriptor.Of("GuardProof", "guard proof", true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("At", "at", ColumnType.Timestamp, false, false);
        return new SchemaDiffTarget(OrmSchema.Of("guard", entity), "guard-v1",
            new("fixture117", ns, ["guard proof"]), new(2, 20, 200, 20, 2000));
    }

    private static async Task TransactionAsync(OrmContext context, DbConnection raw, DbConnection connection,
        ISchemaDiffApplySession session, SchemaDiffArtifact artifact)
    {
        var started = await context.Provider.BeginTransactionAsync(raw, context.DataSource);
        await using var transaction = started.Transaction;
        var before = await session.GuardAndInspectAsync(transaction, default);
        Check.That(before.Digest == artifact.Recipe.Before.Digest, "reobserve after actual writer transaction");
        var create = (CreateTable)artifact.Recipe.Nodes.Up.Single();
        foreach (var statement in await session.PlanStrictCreateAsync(create, transaction, default))
            await ExecuteAsync(connection, transaction, statement.Text);
        var after = await session.GuardAndInspectAsync(transaction, default);
        Check.That(after.Digest == artifact.Recipe.After.Digest, "guarded strict-create physical After");
        await transaction.RollbackAsync();
        await transaction.DisposeAsync();
        if (started.Release is { } release) await release.DisposeAsync();
    }

    private static async Task SetForeignKeysAsync(DbConnection connection, bool value) =>
        await ExecuteAsync(connection, null, value ? "PRAGMA foreign_keys=ON" : "PRAGMA foreign_keys=OFF");

    private static async Task CheckForeignKeysAsync(DbConnection connection, bool expected)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "PRAGMA foreign_keys";
        Check.That(Convert.ToInt64(await command.ExecuteScalarAsync()) == (expected ? 1 : 0), "exact original pragma");
    }

    private static async Task ExecuteAsync(DbConnection connection, DbTransaction? transaction, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql; command.Transaction = transaction;
        await command.ExecuteNonQueryAsync();
    }
}
