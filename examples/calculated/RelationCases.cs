using Neemle.OrmLite.Generated;

namespace CalculatedDemo;

internal static class RelationCases
{
    internal static async Task RunAsync(Fixture f)
    {
        await ChildOrm.InsertAsync(f.Db, new Child { Id = Fixture.Id(10), NoteId = Fixture.Id(1), Title = "CHILD" });
        await TagOrm.InsertAsync(f.Db, new Tag { Id = Fixture.Id(20), Label = "TAG" });
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        await NoteOrm.AddTagAsync(f.Db, connection, Fixture.Id(1), Fixture.Id(20));
        var child = (await ChildOrm.GetAsync(f.Db, Fixture.Id(10)))!;
        await ChildOrm.LoadNoteAsync(f.Db, connection, child);
        var note = child.Note!;
        await NoteOrm.LoadChildrenAsync(f.Db, note);
        await NoteOrm.LoadTagsAsync(f.Db, connection, note);
        var values = string.Join("|", note.LowerLabel, note.Children.Single().LowerTitle, note.Tags.Single().LowerLabel);
        Results.Require(values == "mixed|child|tag", "reference/collection/joined target calculations");
        Results.Case("relations", values);
        var empty = (await NoteOrm.GetAsync(f.Db, Fixture.Id(3)))!;
        await NoteOrm.LoadChildrenAsync(f.Db, empty);
        await NoteOrm.LoadTagsAsync(f.Db, connection, empty);
        Results.Require(empty.Children.Count == 0 && empty.Tags.Count == 0, "empty relations remain empty");
        Results.Case("empty-relations", "0|0");
    }
}
