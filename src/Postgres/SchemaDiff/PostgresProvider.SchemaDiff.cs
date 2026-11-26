using System.Data.Common;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresProvider
{
    public Task<SchemaDiffObservation> InspectAsync(DbConnection connection,
        SchemaDiffInspection request, CancellationToken ct) =>
        PostgresSchemaEntry.RunAsync(() => PostgresSchemaInspect.InspectAsync(connection, request, ct), ct);

    public Task<ISchemaDiffApplySession> BeginDiffApplyAsync(DbConnection connection,
        SchemaDiffInspection request, CancellationToken ct) =>
        PostgresSchemaEntry.RunAsync(() => PostgresSchemaSession.CreateAsync(this, connection, request, ct), ct);
}
