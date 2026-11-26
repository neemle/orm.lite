using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace StreamingExample;

internal static class NoteExport
{
    internal static async Task RunAsync(OrmContext db)
    {
        await db.MigrateAsync();
        await SeedAsync(db);
        Console.WriteLine("Stored notes: " + await NoteOrm.Query(db).CountAsync());
        await ExportAsync(db);
        await ProjectionExport.RunAsync(db);
        var preview = await StreamLifetime.PreviewAsync(NoteOrm.Query(db).OrderBy(n => n.Title).StreamAsync(),
            note => Console.WriteLine("Preview: " + note.Title));
        Console.WriteLine("Preview completion: " + preview.Reason + "; rows=" + preview.RowsYielded);
        await CancellationExport.RunAsync(db);
        await BorrowedExport.RunAsync(db);
        Console.WriteLine("Stored notes after reads: " + await NoteOrm.Query(db).CountAsync());
    }

    private static async Task SeedAsync(OrmContext db)
    {
        await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "alpha", Stars = 5, Estimate = 8 });
        await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "beta", Stars = 3 });
        await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "gamma", Stars = 1, Estimate = 13 });
    }

    private static async Task ExportAsync(OrmContext db)
    {
        var query = NoteOrm.Query(db).Where(n => n.Stars >= 3).OrderBy(n => n.Title);
        var captured = query.StreamAsync();
        query.Take(1);
        Console.WriteLine("Captured export before a later Take(1):");
        Console.WriteLine("title,stars");
        await foreach (var note in captured)
            Console.WriteLine("\"" + note.Title.Replace("\"", "\"\"") + "\"," + note.Stars);
    }
}
