using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.1 C7 (D14.5 clause theory): only the root publishes, after its last child and cleanup; the final
// read of the caller's token decides. The root is held there by a handshake, the caller cancels, and it resumes.
public sealed class PublicationTests
{
    [Theory(DisplayName = "ORML-137 D14 C7 publication: a cancel after the last cleanup is cancellation with the right "
        + "cause, never the result")]
    [InlineData("completed")]
    [InlineData("partial")]
    [InlineData("busy-row")]
    [InlineData("not-wal")]
    [InlineData("gate-busy")]
    [InlineData("contention")]
    [InlineData("failure")]
    [InlineData("vacuum")]
    public async Task C7_Publication(string status)
    {
        var rig = new ExecutionRig();
        var failure = new ScriptedDbException(status, status == "contention");
        rig.Source.Script(Checkpoint, _ => status switch
        {
            "partial" => Row(rig.Source, 0, 4, 2),
            "busy-row" => Row(rig.Source, 1, 4, 2),
            "not-wal" => Row(rig.Source, 0, -1, -1),
            "contention" or "failure" => throw failure,
            _ => Row(rig.Source, 0, 4, 4),
        });
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Hold(rig, status, async () =>
        {
            held.SetResult();
            await resume.Task;
        });

        Task call = status == "vacuum" ? rig.VacuumAsync() : rig.CheckpointAsync();
        await held.Task;
        await rig.Caller.CancelAsync();
        resume.SetResult();

        await rig.CancelledAsync(call, status is "contention" or "failure" ? failure : null);
        rig.AssertUnwound();
    }

    // The root's last effect: the gate itself when it is busy, else the release of the (last) gate.
    private static void Hold(ExecutionRig rig, string status, Func<Task> hold)
    {
        if (status == "gate-busy")
            rig.Provider.Gate = async (_, _) =>
            {
                await hold();
                return new MaintenanceWriteLease(false, null);
            };
        else
            rig.Provider.OnRelease = () =>
                status != "vacuum" || rig.Log.Count("release gate") == 1 ? hold() : Task.CompletedTask;
    }
}
