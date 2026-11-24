#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public static class MigrationRunner
{
    public const string HistoryTable = "schema_migrations";

    public const string VersionColumn = "version";

    private const string VersionParameter = "v";

    public static Node CreateHistoryTable() =>
        new CreateTable(HistoryTable, [
            new ColumnDef(VersionColumn, new TypeRef(ColumnType.Text), false, true),
            new ColumnDef("applied_at", new TypeRef(ColumnType.Timestamp), false, false, Defaults.Now),
        ]);

    public static Node ReadApplied() => new Select([new ColumnRef(VersionColumn)], new TableRef(HistoryTable));

    public static Node RecordApplied(string version) =>
        new InsertData(HistoryTable, DdlLiterals.Columns([VersionColumn]), [new DataRow([new NamedParam(VersionParameter, version)])]);

    public static async Task RunAsync(
        DbDataSource dataSource,
        ISqlProvider provider,
        IEnumerable<MigrationContribution> contributions,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(contributions);

        var all = Ordered(contributions);
        var planner = provider.Planner ?? new StatementPlanner(provider);
        var plan = planner.PlanRun(provider);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        using var actor = DbActor.EnterSystem();
        await provider.StampActorAsync(conn, DbActorValue.System, ct);
        await ExecuteAllAsync(conn, plan.Acquire, ct);
        try
        {
            await ApplyPendingAsync(new Host(conn, dataSource, provider, planner), plan, all, ct);
        }
        finally
        {
            await ExecuteAllAsync(conn, plan.Release, CancellationToken.None);
        }
    }

    private static List<MigrationContribution> Ordered(IEnumerable<MigrationContribution> contributions)
    {
        var all = new List<MigrationContribution>(contributions);
        foreach (var contribution in all) contribution.Require();
        all.Sort((a, b) => string.CompareOrdinal(a.Version, b.Version));
        RefuseRepeatedVersions(all);
        return all;
    }

    private static void RefuseRepeatedVersions(List<MigrationContribution> sorted)
    {
        for (var i = 1; i < sorted.Count; i++)
            if (string.Equals(sorted[i].Version, sorted[i - 1].Version, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"migration version '{sorted[i].Version}' is supplied more than once; "
                    + "a version names one migration, so rename or remove all but one.");
    }

    public static async Task ApplyAsync(
        DbDataSource dataSource, ISqlProvider provider, IReadOnlyList<Node> operations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operations);
        var planner = provider.Planner ?? new StatementPlanner(provider);
        planner.Validate(operations);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        using var actor = DbActor.EnterSystem();
        await provider.StampActorAsync(conn, DbActorValue.System, ct);
        await ApplyUnitAsync(conn, dataSource, provider, planner, operations, null, null, UnitGuard.None, ct);
    }

    public static async Task RevertAsync(
        DbDataSource dataSource, ISqlProvider provider, MigrationContribution contribution, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(provider);
        contribution.Require();
        if (contribution.Reversibility != Reversibility.Reversible)
            throw new InvalidOperationException(
                $"migration {contribution.Version} is declared Irreversible and cannot be reverted; "
                + "nothing was executed.");
        var operations = DownOf(provider, contribution);
        var planner = provider.Planner ?? new StatementPlanner(provider);
        planner.Validate(operations);
        var plan = planner.PlanRun(provider);
        var forget = new DeleteData(HistoryTable, VersionColumn, [new NamedParam(VersionParameter, contribution.Version)]);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        using var actor = DbActor.EnterSystem();
        await provider.StampActorAsync(conn, DbActorValue.System, ct);
        await ExecuteAllAsync(conn, plan.Acquire, ct);
        try
        {
            await ApplyUnitAsync(conn, dataSource, provider, planner, operations, forget, contribution.Version, UnitGuard.RequireApplied, ct);
        }
        finally
        {
            await ExecuteAllAsync(conn, plan.Release, CancellationToken.None);
        }
    }

    private static IReadOnlyList<Node> DownOf(ISqlProvider provider, MigrationContribution contribution)
    {
        if (contribution.Down is { } down)
        {
            var builder = new MigrationBuilder(provider);
            down(builder);
            return builder.Nodes;
        }
        if (contribution.Nodes is { } nodes)
            return MigrationOps.DeriveDown(nodes(provider)) ?? throw new InvalidOperationException(
                $"migration {contribution.Version} has no Down and its operations are not all reversible, so none can be derived.");
        throw new InvalidOperationException($"migration {contribution.Version} has no Down.");
    }

    private static IReadOnlyList<Node> NodesOf(MigrationContribution contribution, ISqlProvider provider) =>
        contribution.Nodes is { } nodes
            ? nodes(provider)
            : contribution.Render(provider).Where(sql => !string.IsNullOrWhiteSpace(sql)).Select(sql => (Node)new RawSql(null, sql)).ToList();

    private readonly record struct Host(
        DbConnection Conn, DbDataSource Source, ISqlProvider Provider, IMigrationPlanner Planner);

    private static async Task ApplyPendingAsync(
        Host host, MigrationRunPlan plan, List<MigrationContribution> all, CancellationToken ct)
    {
        var applied = await ReadHistoryAsync(host, plan, ct);
        var pending = all
            .Where(c => !applied.Contains(c.Version))
            .Select(c => (Contribution: c, Operations: NodesOf(c, host.Provider)))
            .ToList();
        foreach (var (_, operations) in pending) host.Planner.Validate(operations);
        var guard = plan.Exclusive ? UnitGuard.None : UnitGuard.SkipWhenApplied;
        foreach (var (contribution, operations) in pending)
            await ApplyUnitAsync(
                host.Conn, host.Source, host.Provider, host.Planner,
                operations, RecordApplied(contribution.Version), contribution.Version, guard, ct);
    }

    private static async Task<HashSet<string>> ReadHistoryAsync(Host host, MigrationRunPlan plan, CancellationToken ct)
    {
        if (plan.Exclusive) return await BootstrapAsync(host.Conn, null, plan, ct);
        return await InTransactionAsync(
            host.Conn, host.Source, host.Provider, tx => BootstrapAsync(host.Conn, tx, plan, ct), ct);
    }

    private static async Task<HashSet<string>> BootstrapAsync(
        DbConnection conn, DbTransaction? tx, MigrationRunPlan plan, CancellationToken ct)
    {
        foreach (var statement in plan.Bootstrap) await ExecuteAsync(conn, tx, statement, ct);
        return await AppliedAsync(conn, tx, plan.ReadApplied, ct);
    }

    private static async Task ApplyUnitAsync(
        DbConnection conn, DbDataSource source, ISqlProvider provider, IMigrationPlanner planner,
        IReadOnlyList<Node> operations, Node? record, string? version, UnitGuard guard, CancellationToken ct)
    {
        await using var unit = await planner.BeginUnitAsync(conn, operations, ct);
        var started = await provider.BeginTransactionAsync(conn, source, ct);
        try
        {
            await using var tx = started.Transaction;
            using var trace = TransactionTrace.Begin(conn);
            if (guard != UnitGuard.None)
            {
                var present = (await AppliedAsync(conn, tx, StatementCompiler.Compile(ReadApplied(), provider, StatementKind.Read), ct)).Contains(version!);
                if (guard == UnitGuard.SkipWhenApplied && present) return;
                if (guard == UnitGuard.RequireApplied && !present)
                    throw new InvalidOperationException($"migration {version} is not applied, so there is nothing to revert.");
            }
            foreach (var operation in operations)
                foreach (var statement in await unit.PlanAsync(operation, tx, ct))
                    await ExecuteAsync(conn, tx, statement, ct);
            if (record is not null) await ExecuteAsync(conn, tx, Compile(record, provider), ct);
            await unit.VerifyAsync(tx, ct);
            await tx.CommitAsync(ct);
            trace?.Commit();
        }
        finally
        {
            if (started.Release is not null) await started.Release.DisposeAsync();
        }
    }

    private enum UnitGuard
    {
        None,
        SkipWhenApplied,
        RequireApplied,
    }

    private static async Task<T> InTransactionAsync<T>(
        DbConnection conn, DbDataSource source, ISqlProvider provider, Func<DbTransaction, Task<T>> work, CancellationToken ct)
    {
        var started = await provider.BeginTransactionAsync(conn, source, ct);
        try
        {
            await using var tx = started.Transaction;
            using var trace = TransactionTrace.Begin(conn);
            var result = await work(tx);
            await tx.CommitAsync(ct);
            trace?.Commit();
            return result;
        }
        finally
        {
            if (started.Release is not null) await started.Release.DisposeAsync();
        }
    }

    private static Statement Compile(Node op, ISqlProvider provider) => DdlCompiler.Compile(op, provider).Single();

    private static async Task<HashSet<string>> AppliedAsync(DbConnection conn, DbTransaction? tx, Statement statement, CancellationToken ct)
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = statement.Text;
        await using var reader = await cmd.RunReaderAsync(statement.Kind, ct);
        while (await reader.ReadAsync(ct)) applied.Add(reader.GetString(0));
        return applied;
    }

    private static async Task ExecuteAllAsync(DbConnection conn, IReadOnlyList<Statement> statements, CancellationToken ct)
    {
        foreach (var statement in statements) await ExecuteAsync(conn, null, statement, ct);
    }

    private static async Task ExecuteAsync(DbConnection conn, DbTransaction? tx, Statement statement, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = statement.Text;
        cmd.Transaction = tx;
        foreach (var parameter in statement.Parameters) cmd.AddParam(parameter.Name, parameter.Value);
        await cmd.RunNonQueryAsync(statement.Kind, ct);
    }
}
