using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public interface ISchemaDiffApplySession : IAsyncDisposable
{
    Task<SchemaDiffObservation> GuardAndInspectAsync(DbTransaction transaction, CancellationToken ct);
    Task<IReadOnlyList<Statement>> PlanStrictCreateAsync(
        CreateTable operation, DbTransaction transaction, CancellationToken ct);
    Task<SchemaRestoration> RestoreAndVerifyAsync(CancellationToken ct);
}
