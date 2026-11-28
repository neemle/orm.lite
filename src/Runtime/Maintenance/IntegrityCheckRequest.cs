namespace Neemle.OrmLite;

/// <summary>An integrity check in the named mode, of the whole database or of one table.</summary>
/// <param name="Mode">The check; required.</param>
public sealed record IntegrityCheckRequest(IntegrityCheckMode Mode)
{
    /// <summary>The largest <see cref="MaxFindings"/> a request may ask for.</summary>
    public const int MaxFindingsLimit = 10_000;

    /// <summary>The table to check, by its generated descriptor; null checks the whole database.</summary>
    public EntityDescriptor? Table { get; init; }

    /// <summary>How many findings to return, 1 to <see cref="MaxFindingsLimit"/>; more set <see cref="IntegrityCheckResult.Truncated"/>.</summary>
    public int MaxFindings { get; init; } = 100;

    /// <summary>The whole budget of the call; null takes the provider's maintenance timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}
