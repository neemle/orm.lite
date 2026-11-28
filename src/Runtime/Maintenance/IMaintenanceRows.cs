using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>
/// Reduces the rows of a maintenance statement to its result, synchronously: the executor owns every read, so no
/// await exists outside it (ADR D14.2).
/// </summary>
internal interface IMaintenanceRows<out TResult>
{
    /// <summary>Takes the current row; false when no more rows are needed.</summary>
    bool Accept(DbDataReader row);

    /// <summary>The result once the rows are taken.</summary>
    TResult Complete();
}
