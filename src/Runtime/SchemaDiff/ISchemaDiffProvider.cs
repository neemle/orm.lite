using System.Data.Common;

namespace Neemle.OrmLite;

public interface ISchemaDiffProvider
{
    Task<SchemaDiffObservation> InspectAsync(DbConnection connection, SchemaDiffInspection request,
             CancellationToken ct);
    Task<ISchemaDiffApplySession> BeginDiffApplyAsync(
        DbConnection connection, SchemaDiffInspection request, CancellationToken ct);
}
