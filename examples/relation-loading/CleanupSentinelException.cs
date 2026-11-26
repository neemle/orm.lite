namespace PolicySmoke;

internal sealed class CleanupSentinelException(string stage) : Exception(stage);
