namespace PolicySmoke;

internal sealed class CleanupFaultPlan
{
    internal Exception Primary { get; } = new MapSentinelException();
    internal Exception CloseError { get; } = new CleanupSentinelException("close");
    internal Exception DisposeError { get; } = new CleanupSentinelException("dispose");
    internal bool FailClose { get; init; } = true;
    internal bool FailDispose { get; init; } = true;
    internal int Closes { get; set; }
    internal int Disposes { get; set; }
}
