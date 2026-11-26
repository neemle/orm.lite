using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace StreamingExample;

internal static class ProjectionExport
{
    internal static async Task RunAsync(OrmContext db)
    {
        var selected = NoteOrm.Query(db).Where(n => n.Stars >= 3).OrderBy(n => n.Title);
        await foreach (var title in selected.StreamProjectAsync([n => n.Title], reader => reader.GetString(0)))
            Console.WriteLine("Projected title: " + title);
        var total = 0;
        var stars = NoteOrm.Query(db).Where(n => n.Stars >= 3).ProjectColumn(n => n.Stars)
            .StreamProjectionAsync(reader => reader.GetInt32(0));
        await foreach (var value in stars) total += value;
        Console.WriteLine("Projected stars total: " + total);
        await EstimatesAsync(db);
    }

    private static async Task EstimatesAsync(OrmContext db)
    {
        Console.Write("Nullable estimates:");
        await foreach (var value in NoteOrm.Query(db).OrderBy(n => n.Title).StreamColumnAsync<int?>(n => n.Estimate))
            Console.Write(" " + (value?.ToString() ?? "null"));
        Console.WriteLine();
        Console.Write("Defaulted estimates:");
        await foreach (var value in NoteOrm.Query(db).OrderBy(n => n.Title).StreamColumnAsync<int>(n => n.Estimate))
            Console.Write(" " + value);
        Console.WriteLine();
    }
}
