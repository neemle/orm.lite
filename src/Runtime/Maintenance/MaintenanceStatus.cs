namespace Neemle.OrmLite;

/// <summary>How a maintenance operation ended. Members are only ever appended.</summary>
public enum MaintenanceStatus
{
    /// <summary>The operation did all of its work.</summary>
    Completed = 1,

    /// <summary>The operation ran without contention but left work undone, for example WAL frames a reader still needs.</summary>
    Partial,

    /// <summary>The operation met contention it could not wait out within its timeout; it is a result, not an exception.</summary>
    Busy,

    /// <summary>The database is not in WAL mode, so there was nothing to checkpoint.</summary>
    NotWal,

    /// <summary>
    /// A prerequisite step did not complete, so the operation's own statement was not issued; the result carries the
    /// prerequisite's outcome (for VACUUM on SQLite, the WAL checkpoint).
    /// </summary>
    NotRun,
}
