using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveLateProjectionTests
{
    [Fact(DisplayName = "[UC-006][SEC-112-01/02] projected references tagged before save registration cannot be laundered by later registration")]
    public async Task Projection_before_registration_is_still_partial_after_registration()
    {
        await using var db = await SaveProviderDatabase.OpenAsync("sqlite");
        await SaveMaterializerAssertions.SeedAsync(db);
        var rows = await SaveMaterializerAssertions.Query(db).ProjectAsync([n => n.Id, n => n.Name],
            r => new SaveLateProjection { Id = r.GetGuid(0), Name = r.GetString(1) });
        var partial = Assert.Single(rows);
        var descriptor = EntityDescriptor.Of("SaveLateProjection", "gs112_probe_node", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
            .Property("Name", "name", ColumnType.Text, false, false);
        OrmMeta.Register<SaveLateProjection>(descriptor);
        GraphSaveModel.Register(new GraphSaveEntityBinding<SaveLateProjection, Guid>(descriptor,
            n => [n.Id, n.ParentId, n.Name], n => n.Id, Map, []));
        db.ClearEvents();
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => global::Neemle.OrmLite.GraphSave.SaveAsync(db.Context,
            new GraphSaveRequest<SaveLateProjection>(partial, GraphSaveIntent.Update,
                new(GraphSaveIntents.Empty, GraphSavePermissions.None, SaveProbeModel.Room))));
        Assert.Equal(GraphSaveError.UnsupportedMapping, error.Error); Assert.Equal(GraphSaveOutcome.NotStarted, error.Outcome);
        Assert.Empty(db.Statements); Assert.Empty(db.Transactions);
    }
    private static SaveLateProjection Map(DbDataReader r) => new()
    { Id = r.GetGuid(0), ParentId = r.IsDBNull(1) ? null : r.GetGuid(1), Name = r.GetString(2) };
}
