namespace Neemle.OrmLite;

/// <summary>What every maintenance result reports.</summary>
public interface IMaintenanceResult
{
    /// <summary>The operation that produced this result.</summary>
    MaintenanceOperation Operation { get; }

    /// <summary>The id of the provider that ran it.</summary>
    string Provider { get; }

    /// <summary>How the operation ended.</summary>
    MaintenanceStatus Status { get; }

    /// <summary>Exactly <c>Status == MaintenanceStatus.Completed</c>.</summary>
    bool IsComplete { get; }
}
