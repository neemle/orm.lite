using Xunit;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanLateOriginTests
{
    [Fact(DisplayName = "[UC-007][SEC-113-01] a full read before late orphan registration stays partial on same-object reread")]
    public async Task Late_registration_and_a_full_reread_cannot_promote_a_retained_reference()
    {
        OrphanReadModel<DateTime>.Register(false);
        await using var database = await OrphanReadDatabase.Open();
        var row = Assert.Single(await OrphanMaterializerTests.Query<DateTime>(database).ToListAsync());
        OrphanModel.RegisterEntity(OrphanReadModel<DateTime>.Binding);
        OrphanReadAssertions.Refused(database, row);
        OrphanReadModel<DateTime>.Reuse = row;
        try
        {
            var reread = Assert.Single(await OrphanMaterializerTests.Query<DateTime>(database).ToListAsync());
            Assert.Same(row, reread);
            Assert.Equal("root", reread.Name);
            OrphanReadAssertions.Refused(database, reread);
            GraphSaveOrigins.RefuseKnownPartial(reread);
        }
        finally { OrphanReadModel<DateTime>.Reuse = null; }
        OrphanReadAssertions.Full(Assert.Single(await OrphanMaterializerTests.Query<DateTime>(database).ToListAsync()));
    }

    [Fact(DisplayName = "[UC-007][SEC-113-01] complete custom mapper output is conservatively partial for orphan payloads")]
    public async Task A_different_mapper_does_not_gain_the_registered_full_row_witness()
    {
        OrphanReadModel<bool>.Register();
        await using var database = await OrphanReadDatabase.Open();
        var rows = await new Query<OrphanReadRow<bool>>(database.Context, reader => OrphanReadModel<bool>.Map(reader))
            .Where(row => row.Id == OrphanReadDatabase.Root).ToListAsync();
        OrphanReadAssertions.Refused(database, Assert.Single(rows));
    }

    [Theory(DisplayName = "[UC-007][SEC-113-01] noncanonical direct and legacy linked reads remain partial")]
    [InlineData(false)] [InlineData(true)]
    public async Task Noncanonical_direct_and_legacy_linked_reads_cannot_claim_current_full_metadata(bool linked)
    {
        OrphanReadModel<decimal>.Register();
        await using var database = await OrphanReadDatabase.Open();
        await using var connection = await database.Context.DataSource.OpenConnectionAsync();
        var row = linked
            ? Assert.Single(await OrmExec.LoadLinkedAsync(database.Context, connection,
                OrphanReadModel<decimal>.Descriptor, "Peers", OrphanReadDatabase.Root, OrphanReadModel<decimal>.Map))
            : (await OrmExec.GetAsync(database.Context, connection,
                EntityDescriptor.Of("OrphanReadRow", "u1_rows", true)
                    .Property("Id", "id", ColumnType.Uuid, false, true)
                    .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
                    .Property("Name", "name", ColumnType.Text, false, false), OrphanReadDatabase.Root,
                OrphanReadModel<decimal>.Map))!;
        OrphanReadAssertions.Refused(database, row);
    }

    [Fact(DisplayName = "[UC-007][SEC-113-01] scoped policy materialization remains partial before late registration")]
    public async Task A_policy_scope_read_cannot_be_promoted_after_late_registration()
    {
        OrphanReadModel<short>.Register(false);
        await using var database = await OrphanReadDatabase.Open();
        await using var scope = await LoadingScope.OpenAsync(database.Context,
            new LoadingScopeOptions(new(new(2, 2, 4), new(2, 4, 4)), 1, TimeSpan.FromMinutes(1)));
        var rows = await OrphanMaterializerTests.Query<short>(database)
            .ToListAsync(new RelationReadOptions(Scope: scope));
        OrphanModel.RegisterEntity(OrphanReadModel<short>.Binding);
        OrphanReadAssertions.Refused(database, Assert.Single(rows));
    }
}
