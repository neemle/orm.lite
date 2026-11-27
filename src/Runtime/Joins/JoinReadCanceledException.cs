namespace Neemle.OrmLite;

public sealed class JoinReadCanceledException : OperationCanceledException
{
    internal JoinReadCanceledException(CancellationToken token, Exception? cause = null)
        : base("Joined read canceled.", cause, token) { }

    public override string ToString() => nameof(JoinReadCanceledException) + ": " + Message;
}
