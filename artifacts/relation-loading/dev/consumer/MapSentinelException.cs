namespace PolicySmoke;

internal sealed class MapSentinelException : Exception
{
    internal MapSentinelException() : base("fixture mapper failure") { }
}
