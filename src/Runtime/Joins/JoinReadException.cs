namespace Neemle.OrmLite;

public sealed class JoinReadException : Exception
{
    internal JoinReadException(JoinError error, Exception? cause = null)
        : base($"Joined read: {error}.", cause)
    {
        if (!Enum.IsDefined(error)) throw new ArgumentOutOfRangeException(nameof(error));
        Error = error;
    }

    public JoinError Error { get; }
    public override string ToString() => nameof(JoinReadException) + ": " + Message;
}
