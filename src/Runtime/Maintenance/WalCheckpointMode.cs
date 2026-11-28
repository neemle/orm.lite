namespace Neemle.OrmLite;

/// <summary>
/// The SQLite WAL checkpoint mode. There is no default and no zero member: the caller always names one, and it is
/// rendered exactly, never escalated or downgraded.
/// </summary>
public enum WalCheckpointMode
{
    /// <summary>Checkpoints what it can without waiting for readers or writers, and takes no writer gate.</summary>
    Passive = 1,

    /// <summary>Waits for writers, then checkpoints every frame a reader does not still need.</summary>
    Full,

    /// <summary>As <see cref="Full"/>, then waits for readers so the next writer restarts the WAL file.</summary>
    Restart,

    /// <summary>As <see cref="Restart"/>, then truncates the WAL file to zero bytes.</summary>
    Truncate,
}
