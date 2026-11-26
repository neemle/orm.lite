using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal sealed class GraphFixture
{
    internal GraphAuthor[] Authors { get; } =
    [
        new() { Id = Guid.NewGuid(), Name = "0 excluded" },
        new() { Id = Guid.NewGuid(), Name = "1 before the page" },
        new() { Id = Guid.NewGuid(), Name = "2 Alba" },
        new() { Id = Guid.NewGuid(), Name = "3 Bruno" }
    ];

    internal GraphTag[] Tags { get; } =
    [
        new() { Id = Guid.NewGuid(), Label = "Shared" },
        new() { Id = Guid.NewGuid(), Label = "Alba only" }
    ];

    internal GraphBook[] Books { get; }

    internal GraphFixture()
    {
        Books =
        [
            Book(Authors[2], Authors[3], "Edited by Bruno"),
            Book(Authors[2], null, "Without an editor"),
            Book(Authors[3], Authors[3], "Bruno edits his own book")
        ];
    }

    internal async Task SeedAsync(OrmContext db)
    {
        foreach (var author in Authors) await GraphAuthorOrm.InsertAsync(db, author);
        foreach (var book in Books) await GraphBookOrm.InsertAsync(db, book);
        foreach (var tag in Tags) await GraphTagOrm.InsertAsync(db, tag);
        await using var connection = await db.DataSource.OpenConnectionAsync();
        await GraphAuthorOrm.AddTagAsync(db, connection, Authors[2].Id, Tags[0].Id);
        await GraphAuthorOrm.AddTagAsync(db, connection, Authors[2].Id, Tags[1].Id);
        await GraphAuthorOrm.AddTagAsync(db, connection, Authors[3].Id, Tags[0].Id);
    }

    internal async Task<bool> CleanupAsync(OrmContext db)
    {
        await using (var connection = await db.DataSource.OpenConnectionAsync())
        {
            await GraphAuthorOrm.RemoveTagAsync(db, connection, Authors[2].Id, Tags[0].Id);
            await GraphAuthorOrm.RemoveTagAsync(db, connection, Authors[2].Id, Tags[1].Id);
            await GraphAuthorOrm.RemoveTagAsync(db, connection, Authors[3].Id, Tags[0].Id);
        }
        foreach (var book in Books) await GraphBookOrm.DeleteAsync(db, book.Id);
        foreach (var tag in Tags) await GraphTagOrm.DeleteAsync(db, tag.Id);
        foreach (var author in Authors) await GraphAuthorOrm.DeleteAsync(db, author.Id);
        return await GraphAuthorOrm.Query(db).CountAsync() == 0
            && await GraphBookOrm.Query(db).CountAsync() == 0 && await GraphTagOrm.Query(db).CountAsync() == 0;
    }

    private static GraphBook Book(GraphAuthor author, GraphAuthor? editor, string title) => new()
    {
        Id = Guid.NewGuid(), AuthorId = author.Id, EditorId = editor?.Id, Title = title
    };
}
