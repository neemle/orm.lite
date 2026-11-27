using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanMaterializerTests
{
    [Theory(DisplayName = "[UC-007][SEC-113-01] actual masked and projected rows refuse before physical payload access")]
    [InlineData("mask")] [InlineData("project")] [InlineData("projection")]
    [InlineData("stream-mask")] [InlineData("stream-project")] [InlineData("stream-projection")]
    public async Task Partial_public_paths_cannot_be_promoted(string mode)
    {
        OrphanReadModel<int>.Register();
        await using var database = await OrphanReadDatabase.Open();
        var query = Query<int>(database);
        OrphanReadRow<int> row = mode switch
        {
            "mask" => Assert.Single(await query.Redact(r => r.Name).ToListAsync()),
            "project" => Assert.Single(await query.ProjectAsync([r => r.Id, r => r.ParentId], Partial)),
            "projection" => Assert.Single(await query.ProjectColumn(r => r.Id).ProjectColumn(r => r.ParentId)
                .ProjectionAsync(Partial)),
            "stream-mask" => await OrphanReadAssertions.One(query.Redact(r => r.Name).StreamAsync()),
            "stream-project" => await OrphanReadAssertions.One(query.StreamProjectAsync(
                [r => r.Id, r => r.ParentId], Partial)),
            _ => await OrphanReadAssertions.One(query.ProjectColumn(r => r.Id).ProjectColumn(r => r.ParentId)
                .StreamProjectionAsync(Partial)),
        };
        Assert.Equal(OrphanReadDatabase.Root, row.Id);
        OrphanReadAssertions.Refused(database, row);
        Assert.Throws<GraphSaveException>(() => GraphSaveOrigins.RefuseKnownPartial(row));
    }

    [Theory(DisplayName = "[UC-007][AC-113-02] full typed reads retain eligible complete payloads")]
    [InlineData("query")] [InlineData("get")] [InlineData("stream")] [InlineData("linked")]
    public async Task Full_public_paths_remain_full_value_inputs(string mode)
    {
        OrphanReadModel<string>.Register();
        await using var database = await OrphanReadDatabase.Open();
        var row = await FullRead(database, mode);
        Assert.Equal(mode == "linked" ? OrphanReadDatabase.Child : OrphanReadDatabase.Root, row.Id);
        OrphanReadAssertions.Full(row);
    }

    private static async Task<OrphanReadRow<string>> FullRead(OrphanReadDatabase database, string mode)
    {
        if (mode == "query") return Assert.Single(await Query<string>(database).ToListAsync());
        if (mode == "stream") return await OrphanReadAssertions.One(Query<string>(database).StreamAsync());
        var model = OrphanReadModel<string>.Descriptor;
        if (mode == "get") return (await OrmExec.GetAsync(database.Context, model,
            OrphanReadDatabase.Root, OrphanReadModel<string>.Map))!;
        await using var connection = await database.Context.DataSource.OpenConnectionAsync();
        return Assert.Single(await OrmExec.LoadLinkedAsync(database.Context, connection,
            new LinkedRead<OrphanReadRow<string>>(model, "Peers", OrphanReadDatabase.Root,
                model, OrphanReadModel<string>.Map)));
    }

    internal static Query<OrphanReadRow<T>> Query<T>(OrphanReadDatabase database) =>
        new Query<OrphanReadRow<T>>(database.Context, OrphanReadModel<T>.Map)
            .Where(row => row.Id == OrphanReadDatabase.Root);

    private static OrphanReadRow<int> Partial(System.Data.Common.DbDataReader reader) => new()
    { Id = reader.GetGuid(0), ParentId = reader.IsDBNull(1) ? null : reader.GetGuid(1) };
}
