namespace Neemle.OrmLite;

/// <summary>Builds the refusal a guard throws before the replacement writes anything.</summary>
public static class OrphanRefusal
{
    /// <summary>
    /// An <see cref="OrphanException"/> with <paramref name="error"/> and <see cref="OrphanOutcome.NotStarted"/>.
    /// </summary>
    /// <param name="error">The refusal reason.</param>
    /// <returns>The exception to throw.</returns>
    public static OrphanException At(OrphanError error) => new(error, OrphanOutcome.NotStarted);
}
