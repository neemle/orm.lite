using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Generated;
using OrmLiteDemo;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("ORMLITE_DEMO_CONNECTION");
if (string.IsNullOrWhiteSpace(connection)) connection = "Host=localhost;Username=postgres;Database=postgres";

await using var source = NpgsqlDataSource.Create(connection);
var db = new OrmContext(new GatedDataSource(source, source, source));

await OrmLiteDemoMigrations.RunAsync(db.DataSource);
Console.WriteLine("1. tables, indexes, history triggers and the explicit migration are in place");

var welcome = await NoteOrm.GetAsync(db, Guid.Parse(WelcomeNote.Id));
Console.WriteLine($"2. the explicit migration seeded '{welcome?.Title}'");

var tenant = Guid.NewGuid();
var author = Guid.NewGuid();
var note = new Note { Id = Guid.NewGuid(), TenantId = tenant, Title = "first", Body = "hello", Stars = 1 };

using (DbActor.EnterUser(author))
{
    await NoteOrm.InsertAsync(db, note);
    note.Stars = 5;
    await NoteOrm.UpdateAsync(db, note);
}

var liked = await NoteOrm.Query(db).Where(n => n.TenantId == tenant && n.Stars >= 3).OrderBy(n => n.Title).ToListAsync();
Console.WriteLine($"3. inserted, updated and queried with a lambda: {liked.Count} note(s) with 3 or more stars");

using (DbActor.EnterUser(author))
{
    await NoteOrm.DeleteAsync(db, note.Id);
}

var live = await NoteOrm.Query(db).Where(n => n.TenantId == tenant).CountAsync();
var withTrash = await NoteOrm.Query(db).IncludingDeleted().Where(n => n.TenantId == tenant).CountAsync();
Console.WriteLine($"4. deleted: {live} live row(s), {withTrash} row(s) including the trash");

using (DbActor.EnterUser(author))
{
    await NoteOrm.Query(db).RestoreAsync([note.Id], tenant);
}

var restored = await NoteOrm.Query(db).Where(n => n.TenantId == tenant).CountAsync();
Console.WriteLine($"5. restored: {restored} live row(s)");

var history = await RowHistoryRead.VersionsAsync(db, NoteOrm.Table, note.Id);
foreach (var version in history)
    Console.WriteLine($"   history: {version.Op} by {version.ActorKind} {version.ActorUserId}");

var ok = liked.Count == 1 && live == 0 && withTrash == 1 && restored == 1
    && welcome is not null && history.Select(v => v.Op).Order(StringComparer.Ordinal).SequenceEqual(["restore", "soft_delete", "update"])
    && history.All(v => v.ActorUserId == author);
Console.WriteLine(ok ? "demo ok" : "demo FAILED");
return ok ? 0 : 1;
