# Quickstart with SQLite

[Documentation](README.md)

Create a console app that stores notes in SQLite. You only need the .NET 10 SDK.

## 1. Install the SQLite package

From any directory, create a .NET 10 console app and add the SQLite provider.

```bash
dotnet new console -n Notes -f net10.0
cd Notes
dotnet add package Neemle.OrmLite.Sqlite --version 0.1.0
```

## 2. Define an entity

Create `Note.cs`:

```csharp
using Neemle.OrmLite;

namespace Notes;

[Table("notes")]
public sealed class Note
{
    [Key] public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public int Stars { get; set; }
}
```

## 3. Create the database and run a query

Replace `Program.cs`:

```csharp
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Notes;

await using var db = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = "notes.db" });
await db.MigrateAsync();

await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "hello", Stars = 5 });
var best = await NoteOrm.Query(db).Where(n => n.Stars >= 3).ToListAsync();
foreach (var note in best) Console.WriteLine(note.Title);
```

## 4. Run the app

```bash
dotnet run
```

On the first run, ORM.lite creates `notes.db` and the notes table, inserts a row, and prints:

```text
hello
```

`NoteOrm` comes from the source generator. `db.MigrateAsync()` applies the generated baseline and any
registered [migrations](migrations.md).

Next: [configure the connection](configuration.md) or [write more queries](queries.md).
