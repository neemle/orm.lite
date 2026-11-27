namespace Neemle.OrmLite;

/// <summary>A WAL checkpoint in the named mode.</summary>
/// <param name="Mode">The checkpoint mode; required.</param>
public sealed record WalCheckpointRequest(WalCheckpointMode Mode)
{
    /// <summary>
    /// The whole budget of the call, gate wait and statement included; null takes the provider's maintenance timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
