using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanRegistrationTests
{
    [Fact(DisplayName = "[UC-007][SEC-113-01] equivalent entity registration is idempotent and conflicts preserve the first binding")]
    public void Conflicting_registration_cannot_replace_the_original()
    {
        var descriptor = OrphanReadModel<char>.Descriptor;
        OrmMeta.Register<OrphanReadRow<char>>(descriptor);
        var original = new OrphanEntityBinding<OrphanReadRow<char>, Guid>(descriptor,
            OrphanReadModel<char>.Values, Key);
        OrphanModel.RegisterEntity(original);
        OrphanModel.RegisterEntity(new OrphanEntityBinding<OrphanReadRow<char>, Guid>(descriptor,
            OrphanReadModel<char>.Values, Key));
        Refused(() => OrphanModel.RegisterEntity(new OrphanEntityBinding<OrphanReadRow<char>, Guid>(descriptor,
            row => [row.Id, row.ParentId, "replacement"], Key)));
        var row = new OrphanReadRow<char> { Id = OrphanReadDatabase.Root, Name = "original" };
        Assert.Equal([row.Id, null, "original"], ((IOrphanEntity)original).Values(row));
        Assert.True(OrphanModel.CurrentForRead(typeof(OrphanReadRow<char>)));
    }

    private static Guid Key(OrphanReadRow<char> row) => row.Id;

    [Theory(DisplayName = "[UC-007][SEC-113-01] reference or structural metadata drift refuses before physical getters")]
    [InlineData(false)] [InlineData(true)]
    public void Frozen_metadata_cannot_be_replaced_or_widened(bool structural)
    {
        if (structural)
        {
            OrphanReadModel<byte>.Register();
            OrphanReadModel<byte>.Descriptor.Property("Extra", "extra", ColumnType.Text, true, false);
            BeforeValues(OrphanReadModel<byte>.Binding, new OrphanReadRow<byte>());
        }
        else
        {
            OrphanReadModel<uint>.Register();
            OrmMeta.Register<OrphanReadRow<uint>>(EquivalentDescriptor());
            BeforeValues(OrphanReadModel<uint>.Binding, new OrphanReadRow<uint>());
        }
    }

    [Theory(DisplayName = "[UC-007][SEC-113-01] every read-policy hook prevents plain orphan admission")]
    [InlineData("rows")] [InlineData("first")] [InlineData("snapshot")]
    public void All_policy_hooks_refuse_before_payload_access(string hook)
    {
        OrphanReadModel<ushort>.Register();
        if (hook == "rows") PolicyReadHooks<OrphanReadRow<ushort>>.Rows = (_, _, _) => Task.FromResult<List<OrphanReadRow<ushort>>>([]);
        if (hook == "first") PolicyReadHooks<OrphanReadRow<ushort>>.First = (_, _, _) => Task.FromResult<OrphanReadRow<ushort>?>(null);
        if (hook == "snapshot") PolicyReadHooks<OrphanReadRow<ushort>>.Snapshot = (_, _, _) => Task.FromResult<List<OrphanReadRow<ushort>>>([]);
        try
        {
            BeforeValues(OrphanReadModel<ushort>.Binding, new OrphanReadRow<ushort>(), OrphanError.UnsupportedVisibility);
            Refused(() => OrphanModel.RegisterEntity(OrphanReadModel<ushort>.Binding), OrphanError.UnsupportedVisibility);
        }
        finally
        {
            PolicyReadHooks<OrphanReadRow<ushort>>.Rows = null;
            PolicyReadHooks<OrphanReadRow<ushort>>.First = null;
            PolicyReadHooks<OrphanReadRow<ushort>>.Snapshot = null;
        }
    }

    [Theory(DisplayName = "[UC-007][AC-113-04] incompatible or nullable physical key shapes never register")]
    [InlineData(false)] [InlineData(true)]
    public void Invalid_key_shapes_refuse_without_getters(bool nullable)
    {
        var descriptor = EntityDescriptor.Of("BadKey", "u1_bad_key", true)
            .Property("Id", "id", ColumnType.Uuid, nullable, true);
        if (nullable) Refused(() => new OrphanEntityBinding<OrphanReadRow<ulong>, Guid>(descriptor,
            _ => throw new InvalidOperationException("payload getter"), _ => throw new InvalidOperationException("key getter")));
        else Refused(() => new OrphanEntityBinding<OrphanReadRow<ulong>, int>(descriptor,
            _ => throw new InvalidOperationException("payload getter"), _ => throw new InvalidOperationException("key getter")));
    }

    [Fact(DisplayName = "[UC-007][SEC-113-01] protected tenant visibility cannot acquire plain physical bindings")]
    public void Protected_visibility_refuses_before_any_value_or_key_getter()
    {
        var descriptor = EquivalentDescriptor().SoftDelete("Deleted", "deleted", "u1").Tenant("tenant_id");
        Refused(() => new OrphanEntityBinding<OrphanReadRow<sbyte>, Guid>(descriptor,
            _ => throw new InvalidOperationException("payload getter"), _ => throw new InvalidOperationException("key getter")),
            OrphanError.UnsupportedVisibility);
    }

    [Fact(DisplayName = "[UC-007][AC-113-04] multiple physical foreign keys on the owner column cannot become an owned relation")]
    public void Ambiguous_physical_owner_fk_refuses_before_payload_access()
    {
        OrphanReadModel<float>.Register();
        var descriptor = EquivalentDescriptor()
            .ForeignKey(new("fk_one", "parent_id", new("u1_rows", "id"), ReferentialAction.Cascade))
            .ForeignKey(new("fk_two", "parent_id", new("u1_rows", "id"), ReferentialAction.Restrict));
        OrmMeta.Register<OrphanReadRow<double>>(descriptor);
        var child = new OrphanEntityBinding<OrphanReadRow<double>, Guid>(descriptor,
            _ => throw new InvalidOperationException("payload getter"), _ => throw new InvalidOperationException("key getter"));
        Refused(() => OrphanModel.RegisterCollection(OrphanReadModel<float>.Binding, child, new("Children", "ParentId")));
    }

    [Fact(DisplayName = "[UC-007][SEC-113-01] a stale graph descriptor cannot grant current orphan full provenance")]
    public async Task A_graph_descriptor_with_equal_shape_but_wrong_reference_is_not_a_full_witness()
    {
        OrphanReadModel<TimeSpan>.Register(false);
        var current = EquivalentDescriptor();
        OrmMeta.Register<OrphanReadRow<TimeSpan>>(current);
        var binding = new OrphanEntityBinding<OrphanReadRow<TimeSpan>, Guid>(current,
            OrphanReadModel<TimeSpan>.Values, static row => row.Id);
        OrphanModel.RegisterEntity(binding);
        await using var database = await OrphanReadDatabase.Open();
        var row = Assert.Single(await OrphanMaterializerTests.Query<TimeSpan>(database).ToListAsync());
        BeforeValues(binding, row, OrphanError.UnsupportedVisibility);
    }

    private static EntityDescriptor EquivalentDescriptor() => EntityDescriptor.Of("OrphanReadRow", "u1_rows", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
        .Property("Name", "name", ColumnType.Text, false, false)
        .Link("Peers", "u1_links", "from_id", "to_id", "u1_rows", "id", null, true);

    private static void BeforeValues<T>(OrphanEntityBinding<OrphanReadRow<T>, Guid> binding,
        OrphanReadRow<T> row, OrphanError error = OrphanError.UnsupportedMapping)
    {
        var before = row.ValuesRead;
        Refused(() => ((IOrphanEntity)binding).Values(row), error);
        Assert.Equal(before, row.ValuesRead);
    }

    private static void Refused(Action action, OrphanError error = OrphanError.UnsupportedMapping)
    {
        var failure = Assert.Throws<OrphanException>(action);
        Assert.Equal(error, failure.Error);
        Assert.Equal(OrphanOutcome.NotStarted, failure.Outcome);
    }
}
