// What this file shows
//   The whole demo, top to bottom: load settings, open a database, create or upgrade its schema, use it, and check
//   what came back. Each numbered line it prints is one "chapter"; the program ends with the line "demo ok" and exit code
//   0 only if every check passed, otherwise "demo FAILED" and exit code 1. Nothing here names a database: the same
//   code runs on SQLite (the default, no setup) and on PostgreSQL (change the "OrmLite" settings; see README.md).
//
// Library features exercised in this file
//   OrmLiteOptions.Load(): reads the "OrmLite" block of appsettings.json, then lets OrmLite__* environment variables
//       override it setting by setting.
//   OrmLite.OpenAsync(options): picks the provider the settings name (the package must be referenced: this demo
//       references both) and returns an OrmContext, here called db.
//   db.MigrateAsync(): applies every migration not yet recorded in the schema_migrations table, in version order: the
//       ones the source generator wrote from the [Table] classes and the hand-written [Migration] classes.
//   The generated repositories (NoteOrm and the others, namespace Neemle.OrmLite.Generated): typed Insert, Update,
//       Get, Delete and Query, written at build time from the entity classes. No SQL is written anywhere in the demo.
//   Query<T>: lambdas (n => n.Title == title) are turned into a tree of nodes and then into SQL by the provider.
//   Diagnostics sink: see SlowQueryReport.cs.
//
// How to read the output (the chapters, in the order they run; the numbers are the ones in the printed lines)
//   1       Program.cs   connect and migrate. Prints the provider id, 'sqlite' or 'postgres'.
//   2       Program.cs   an explicit migration (Note.cs, WelcomeNote) seeded a row; prints its title 'welcome'.
//   3       Program.cs   insert, update, read back and a lambda query: prints "1 note(s) with 3 or more stars".
//   4       Program.cs   delete: prints "0 note(s) left".
//   5-7     Trash.cs     soft delete (0 live, 1 with trash), restore (1 live), and the change history with the user who
//                        made each change (restore, soft_delete, update; the user id is random per run).
//   8       Tracker/     the tracker schema built by nine migrations, read back from the database catalog: which
//                        columns exist, that old rows kept their data and got column defaults.
//   9       Tracker/     a unit of work: one commit, one refused change that rolls back, one failure partway that rolls back.
//   10      Tracker/     indexes: how each provider writes a descending composite index (the same text on both here),
//                        the indexes declared on Note, and a unique index refusing a duplicate.
//   11      Tracker/     several aggregates (count, sum, min, distinct count, filtered counts) in one statement.
//   12      Tracker/     one statement with three nested EXISTS across four tables.
//   13      Tracker/     JSON: filters, ordering and in-place changes on a JSON document column.
//   14      Tracker/     views: create (in a migration), read, replace, and a refused write.
//   15      Tracker/     deleting a team removes its projects, items and comments through three cascading foreign keys.
//   16      Tracker/     grouped reports: GROUP BY with HAVING and ordering by an aggregate.
//   17      Ledger.cs    money with exact decimals: transfers between two accounts in a unit of work.
//   18      SlowQueryReport.cs  how many statements the library ran and the three slowest. The count and the times
//                        change on every run and differ between providers, so they are not compared.
//   Then "demo ok". Apart from the provider id in 1, the user id in 7 and the counts and times in 18, the printed
//   lines are the same on SQLite and on PostgreSQL. Chapters 8 to 16 live in Tracker/Chapters.cs; their schema is
//   Tracker/Migrations.cs and Tracker/Entities.cs, and the views are Tracker/Views.cs. demo/README.md says which
//   file shows which chapter.

using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using OrmLiteDemo;

if (args.FirstOrDefault() == "graph") return await GraphDemo.RunAsync(args.Skip(1).ToArray());

// The settings: provider and credentials come from appsettings.json (next to the program) and OrmLite__* environment
// variables, never from code.
var options = OrmLiteOptions.Load();
// The diagnostics sink is an object, so it is attached in code. appsettings.json asks for every statement to be reported
// (see README.md), and reporting without a sink is a start-up error, so this has to happen before OpenAsync.
var report = new SlowQueryReport();
options.Diagnostics.Sink = report;

// On SQLite with no file configured, make a temporary database file (removed at the end). Does nothing otherwise.
using var scratch = ScratchDatabase.IfNoneConfigured(options);
bool ok;
// Open the database. `await using` closes the library's connections when the block ends.
await using (var db = await OrmLite.OpenAsync(options))
{
    // Create or upgrade the schema: runs the generated baseline (the tables of the [Table] classes that are Managed),
    // the generated indexes, then every [Migration] (0100_..., 0200_... to 0280_...) in version order, then the generated
    // reconciliations. A migration that is already recorded is skipped, so this is safe to run again.
    await db.MigrateAsync();
    Console.WriteLine($"1. connected to '{db.Provider.Id}' and applied the migrations");

    // The row that WelcomeNote (Note.cs) inserted while migrating. GetAsync returns null if there is no such key.
    var welcome = await NoteOrm.GetAsync(db, Guid.Parse(WelcomeNote.Id));
    Console.WriteLine($"2. the explicit migration seeded '{welcome?.Title}'");

    // The title carries a fresh GUID because the notes table has a unique index on title (Note.cs): a second run against a
    // database that kept its rows must not insert the same title twice.
    var title = "first " + Guid.NewGuid().ToString("N");
    var note = new Note { Id = Guid.NewGuid(), Title = title, Body = "hello", Stars = 1 };
    // Insert, change a property and write the whole row back, then read it by key.
    await NoteOrm.InsertAsync(db, note);
    note.Stars = 5;
    await NoteOrm.UpdateAsync(db, note);
    var saved = await NoteOrm.GetAsync(db, note.Id);
    // A query written as a lambda; the library turns the condition into SQL and runs it in the database, not over
    // objects in memory.
    var liked = await NoteOrm.Query(db).Where(n => n.Title == title && n.Stars >= 3).OrderBy(n => n.Title).ToListAsync();
    Console.WriteLine($"3. inserted, updated and queried with a lambda: {liked.Count} note(s) with 3 or more stars");

    // Delete by key, then count what is left with that title (a database-side COUNT).
    await NoteOrm.DeleteAsync(db, note.Id);
    var remaining = await NoteOrm.Query(db).Where(n => n.Title == title).CountAsync();
    Console.WriteLine($"4. deleted: {remaining} note(s) left");

    // The other chapters, in the order of the output. Each prints its own lines and returns true when everything it read
    // back was what it expected.
    var trash = await TrashChapter.RunAsync(db);
    var tracker = await TrackerChapters.RunAsync(db);
    var ledger = await LedgerChapter.RunAsync(db);
    var reported = report.Print(top: 3);

    // "demo ok" requires every value read back to be what was written, and every chapter to have passed.
    ok = welcome?.Title == "welcome"
        && saved?.Stars == 5
        && liked.Count == 1 && liked[0].Id == note.Id
        && remaining == 0
        && trash
        && tracker
        && ledger
        && reported;
}

// The scratch folder (if one was made) must be gone too.
ok = scratch.Remove() && ok;
Console.WriteLine(ok ? "demo ok" : "demo FAILED");
return ok ? 0 : 1;
