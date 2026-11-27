namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveFaultRelease(SaveFaultProbe probe) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        probe.Releases++; probe.Events.Add("release");
        return probe.Mode is "postcommit" or "multi-cleanup"
            ? ValueTask.FromException(probe.ReleaseError) : ValueTask.CompletedTask;
    }
}
