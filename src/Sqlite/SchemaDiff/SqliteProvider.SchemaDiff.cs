using System.Data.Common;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider
{
    public Task<SchemaDiffObservation> InspectAsync(DbConnection connection,
        SchemaDiffInspection request, CancellationToken ct) =>
        SqliteSchemaEntry.RunAsync(() => SqliteSchemaInspect.InspectAsync(connection, request, ct), ct);

    public Task<ISchemaDiffApplySession> BeginDiffApplyAsync(DbConnection connection,
        SchemaDiffInspection request, CancellationToken ct) =>
        SqliteSchemaEntry.RunAsync(() => SqliteSchemaSession.CreateAsync(this, connection, request, ct), ct);
}
