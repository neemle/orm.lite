using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class JoinDemoFixture
{
    internal static readonly Guid Aster = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid Birch = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Cedar = Guid.Parse("10000000-0000-0000-0000-000000000003");

    internal static async Task CreateAsync(OrmContext db)
    {
        var schema = OrmSchema.Of("demo_joins", GraphAuthorOrm.Descriptor, GraphBookOrm.Descriptor,
            GraphTagOrm.Descriptor);
        await db.MigrateAsync(schema.Contributions());
        await GraphAuthorOrm.InsertAsync(db, new GraphAuthor { Id = Aster, Name = "Aster" });
        await GraphAuthorOrm.InsertAsync(db, new GraphAuthor { Id = Birch, Name = "Birch" });
        await GraphAuthorOrm.InsertAsync(db, new GraphAuthor { Id = Cedar, Name = "Cedar" });
        await BookAsync(db, "20000000-0000-0000-0000-000000000001", Aster, "Alpha", null);
        await BookAsync(db, "20000000-0000-0000-0000-000000000002", Aster, "Beta", Birch);
        await BookAsync(db, "20000000-0000-0000-0000-000000000003", Birch, "Gamma", null);
    }

    private static Task BookAsync(OrmContext db, string id, Guid author, string title, Guid? editor) =>
        GraphBookOrm.InsertAsync(db, new GraphBook
        { Id = Guid.Parse(id), AuthorId = author, Title = title, EditorId = editor });
}
