using Neemle.OrmLite;

namespace FaultProof;

internal static class FaultWitness
{
    internal static async Task RunAsync(FaultKind kind)
    {
        var control = new FaultControl(kind); control.Prepare();
        await using var source = new FaultSource(control);
        await using var context = new OrmContext(source, new FaultProvider(control));
        var request = new SchemaDiffFixtureRequest(control.Artifact, SchemaDiffFixtureMode.UpgradeObserved,
            new(control.Artifact.ReviewDigest, "fake-isolated117"), new(control.Target.Schema, []));
        var pending = SchemaDiffFixtureRunner.RunAsync(context, request, control.Cancellation.Token);
        if (kind == FaultKind.IgnoredCancel)
        {
            await control.Entered.Task; control.Cancellation.Cancel();
            Require(!pending.IsCompleted, "await actual provider settlement"); control.Release.TrySetResult();
        }
        Exception? failure = null;
        try { await pending; } catch (Exception error) { failure = error; }
        Require(failure is not null, "injected failure surfaced");
        Require(!failure!.ToString().Contains(FaultControl.Canary, StringComparison.Ordinal)
            && !failure.Message.Contains(FaultControl.Canary, StringComparison.Ordinal), "safe default diagnostic");
        Validate(kind, failure, control);
        foreach (var name in new[] { "rollback", "transaction-dispose", "provider-release", "unit-dispose",
                     "session-restore", "session-dispose", "run-lock-release", "connection-dispose" })
            Require(control.Events.Contains(name), "cleanup attempted " + name);
        Console.WriteLine("INJECTED_FAULT_PASS " + kind);
    }

    private static void Validate(FaultKind kind, Exception failure, FaultControl control)
    {
        if (kind is FaultKind.Cancel or FaultKind.IgnoredCancel)
        {
            var canceled = failure as SchemaDiffCanceledException;
            Require(canceled?.CancellationToken == control.Cancellation.Token, "caller token preserved");
            Require(canceled!.ApplyResult!.Units.Single().Outcome == SchemaUnitOutcome.RolledBack,
                "known precommit cancellation rollback");
            if (kind == FaultKind.IgnoredCancel)
                Require(control.Events.IndexOf("ignored-cancel-settled") < control.Events.LastIndexOf("rollback"),
                    "cleanup follows actual provider settlement");
            return;
        }
        var known = failure as SchemaDiffException;
        var expected = kind switch
        {
            FaultKind.CommitAfter => SchemaDiffError.CommitUnknown,
            FaultKind.Rollback or FaultKind.Composite => SchemaDiffError.RollbackUnknown,
            FaultKind.Restore => SchemaDiffError.RestorationUnknown,
            _ => SchemaDiffError.ProviderFailure,
        };
        Require(known?.Error == expected, "exact uncertainty category");
        Require(known!.ApplyResult!.Units.Single().Outcome == Outcome(kind), "per-unit truth");
        Require(control.Created == (kind is FaultKind.CommitAfter or FaultKind.Restore), "fixture commit truth");
        if (kind == FaultKind.Composite) Require(known.CleanupErrors.Count >= 4, "all cleanup errors retained");
    }

    private static SchemaUnitOutcome Outcome(FaultKind kind) => kind switch
    {
        FaultKind.CommitAfter or FaultKind.Rollback or FaultKind.Composite => SchemaUnitOutcome.Unknown,
        FaultKind.Restore => SchemaUnitOutcome.Committed,
        _ => SchemaUnitOutcome.RolledBack,
    };

    private static void Require(bool value, string reason)
    {
        if (!value) throw new IOException("contract witness " + reason);
    }
}
