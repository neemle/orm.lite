using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveRegistrationSafetyTests
{
    [Fact(DisplayName = "[UC-006][SEC-112-01] a conflicting registration cannot replace the original type binding")]
    public async Task A_conflicting_binding_does_not_replace_the_original()
    {
        var descriptor = SaveRegistrationRow<int>.Descriptor();
        OrmMeta.Register<SaveRegistrationRow<int>>(descriptor);
        var binding = SaveRegistrationRow<int>.Binding(descriptor);
        GraphSaveModel.Register(binding);
        GraphSaveModel.Register(SaveRegistrationRow<int>.Binding(descriptor));
        var conflicting = new GraphSaveEntityBinding<SaveRegistrationRow<int>, Guid>(descriptor,
            row => [row.Id, "replacement"], SaveRegistrationRow<int>.Key, SaveRegistrationRow<int>.Map, []);
        var refusal = Assert.Throws<GraphSaveException>(() => GraphSaveModel.Register(conflicting));
        Assert.Equal(GraphSaveError.UnsupportedMapping, refusal.Error);
        await Admission(new SaveRegistrationRow<int>(), true);
    }

    [Fact(DisplayName = "[UC-006][SEC-112-01] replacing metadata by an equal new descriptor still refuses before open")]
    public async Task Descriptor_reference_drift_refuses_before_io()
    {
        var original = SaveRegistrationRow<string>.Descriptor();
        OrmMeta.Register<SaveRegistrationRow<string>>(original);
        GraphSaveModel.Register(SaveRegistrationRow<string>.Binding(original));
        OrmMeta.Register<SaveRegistrationRow<string>>(SaveRegistrationRow<string>.Descriptor());
        try { await Admission(new SaveRegistrationRow<string>(), false); }
        finally { OrmMeta.Register<SaveRegistrationRow<string>>(original); }
    }

    [Fact(DisplayName = "[UC-006][SEC-112-01] mutating a registered descriptor cannot widen a frozen save shape")]
    public async Task Descriptor_shape_drift_refuses_before_io()
    {
        var descriptor = SaveRegistrationRow<long>.Descriptor();
        OrmMeta.Register<SaveRegistrationRow<long>>(descriptor);
        GraphSaveModel.Register(SaveRegistrationRow<long>.Binding(descriptor));
        descriptor.Property("Extra", "extra", Nodes.ColumnType.Text, true, false);
        await Admission(new SaveRegistrationRow<long>(), false);
    }

    [Fact(DisplayName = "[UC-006][SEC-112-02] a known partial value remains refused with an admitted current binding")]
    public async Task Current_registration_cannot_promote_a_partial_value()
    {
        var descriptor = SaveRegistrationRow<Guid>.Descriptor();
        OrmMeta.Register<SaveRegistrationRow<Guid>>(descriptor);
        GraphSaveModel.Register(SaveRegistrationRow<Guid>.Binding(descriptor));
        var row = new SaveRegistrationRow<Guid>();
        GraphSaveOrigins.Observe(row,
            (Func<System.Data.Common.DbDataReader, SaveRegistrationRow<Guid>>)SaveRegistrationRow<Guid>.Map, true);
        await Admission(row, false);
    }

    [Fact(DisplayName = "[UC-006][SEC-112-01] read-policy metadata does not acquire a plain save registration")]
    public void A_policy_bearing_type_cannot_register()
    {
        var descriptor = SaveRegistrationRow<bool>.Descriptor();
        OrmMeta.Register<SaveRegistrationRow<bool>>(descriptor);
        PolicyReadHooks<SaveRegistrationRow<bool>>.Snapshot = (_, _, _) =>
            Task.FromResult<List<SaveRegistrationRow<bool>>>([]);
        try
        {
            var error = Assert.Throws<GraphSaveException>(() =>
                GraphSaveModel.Register(SaveRegistrationRow<bool>.Binding(descriptor)));
            Assert.Equal(GraphSaveError.UnsupportedMapping, error.Error);
            Assert.Equal(GraphSaveOutcome.NotStarted, error.Outcome);
        }
        finally { PolicyReadHooks<SaveRegistrationRow<bool>>.Snapshot = null; }
    }

    private static async Task Admission<T>(T row, bool admitted) where T : class
    {
        await using var source = new SaveProbeSource();
        await using var context = new OrmContext(source, PostgresProvider.Instance);
        var request = new GraphSaveRequest<T>(row, GraphSaveIntent.Insert,
            new(GraphSaveIntents.Empty, GraphSavePermissions.None, SaveProbeModel.Room));
        var error = await Assert.ThrowsAsync<GraphSaveException>(() =>
            global::Neemle.OrmLite.GraphSave.SaveAsync(context, request));
        Assert.Equal(GraphSaveOutcome.NotStarted, error.Outcome);
        Assert.Equal(admitted ? GraphSaveError.ProviderFailure : GraphSaveError.UnsupportedMapping, error.Error);
        Assert.Equal(admitted ? 1 : 0, source.Opens);
        if (admitted) Assert.Same(source.Stop, error.InnerException);
    }
}
