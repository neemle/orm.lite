namespace Neemle.OrmLite;

/// <summary>Helpers over maintenance results.</summary>
public static class MaintenanceResultExtensions
{
    /// <summary>
    /// Throws <see cref="MaintenanceIncompleteException"/> unless <paramref name="result"/> is complete, for a caller
    /// that wants an incomplete run (Busy, Partial, NotRun, ...) to be an exception.
    /// </summary>
    public static void ThrowIfNotComplete(this IMaintenanceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsComplete) throw new MaintenanceIncompleteException(result);
    }
}
