namespace FaultProof;

internal sealed class FaultRelease(FaultControl control) : IAsyncDisposable
{
    public ValueTask DisposeAsync() { control.Events.Add("provider-release"); return ValueTask.CompletedTask; }
}
