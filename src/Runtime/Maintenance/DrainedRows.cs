using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>The reducer of a statement whose rows carry nothing: every row is read, and the result is fixed.</summary>
internal sealed class DrainedRows<TResult>(Func<TResult> completed) : IMaintenanceRows<TResult>
{
    public bool Accept(DbDataReader row) => true;

    public TResult Complete() => completed();
}
