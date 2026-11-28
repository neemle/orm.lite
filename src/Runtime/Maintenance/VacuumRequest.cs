namespace Neemle.OrmLite;

/// <summary>A VACUUM in the named mode, of the whole database or of one table.</summary>
/// <param name="Mode">The VACUUM mode; required.</param>
public sealed record VacuumRequest(VacuumMode Mode)
{
    /// <summary>The table to vacuum, by its generated descriptor; null vacuums the whole database.</summary>
    public EntityDescriptor? Table { get; init; }

    /// <summary>
    /// The whole budget of the call (the checkpoint, every gate wait and the VACUUM itself); null takes the provider's
    /// maintenance timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
