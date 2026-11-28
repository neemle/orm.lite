namespace Neemle.OrmLite;

/// <summary>Thrown by <see cref="MaintenanceResultExtensions.ThrowIfNotComplete"/> for a result that is not complete.</summary>
public sealed class MaintenanceIncompleteException(IMaintenanceResult result)
    : Exception("maintenance operation " + result.Operation + " on provider '" + result.Provider + "' ended " + result.Status + ", not Completed.")
{
    /// <summary>The result that was not complete.</summary>
    public IMaintenanceResult Result { get; } = result;
}
