using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace StreamingExample;

internal static class BorrowedExport
{
    internal static async Task RunAsync(OrmContext db)
    {
        await using var unit = await UnitOfWork.BeginAsync(db);
        var stream = NoteOrm.Query(db).On(unit).OrderBy(n => n.Title).StreamAsync();
        var end = await StreamLifetime.PreviewAsync(stream,
            note => Console.WriteLine("Borrowed preview: " + note.Title));
        var count = await NoteOrm.Query(db).On(unit).CountAsync();
        await unit.CommitAsync();
        Console.WriteLine("Borrowed completion: " + end.Reason + "; rows=" + end.RowsYielded);
        Console.WriteLine("Borrowed count after disposal: " + count + "; committed=" + unit.Committed);
    }
}
