# Quickstart with SQLite

[Documentation](README.md)

Create a console app that stores notes in SQLite. Run the packing command from
[Installation](installation.md) first. Then run these commands from the ORM.lite repository root.

1. Make a project and add the package. Until the packages are on a public feed, point NuGet at the folder that
   `dotnet pack` filled (see [Installation](installation.md)):

       ormlite_feed="$PWD/artifacts"
       ormlite_sample="$(mktemp -d)"
       dotnet new console -n Notes -o "$ormlite_sample" -f net10.0
       cd "$ormlite_sample"
       dotnet new nugetconfig
       dotnet nuget add source "$ormlite_feed" --name orm-lite-local --configfile nuget.config
       dotnet add package Neemle.OrmLite.Sqlite --prerelease

2. Describe a table by decorating a class (`Note.cs`):

       using Neemle.OrmLite;

       namespace Notes;

       [Table("notes")]
       public sealed class Note
       {
           [Key] public Guid Id { get; set; }
           public string Title { get; set; } = "";
           public int Stars { get; set; }
       }

3. Open the database, create the schema, use it (`Program.cs`):

       using Neemle.OrmLite;
       using Neemle.OrmLite.Generated;
       using Notes;

       await using var db = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = "notes.db" });
       await db.MigrateAsync();

       await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "hello", Stars = 5 });
       var best = await NoteOrm.Query(db).Where(n => n.Stars >= 3).ToListAsync();
       foreach (var note in best) Console.WriteLine(note.Title);

   `dotnet run` creates `notes.db`, creates the table, inserts the row and prints `hello`. There is no SQL anywhere
   and no connection class to construct: the provider is a setting, the repository `NoteOrm` is generated from the
   class, and `db.MigrateAsync()` applies the generated baseline and your own `[Migration]` classes, which the build
   registered for the application's assembly (see [Migrations](migrations.md)).

Next: [configure the connection](configuration.md) or [write more queries](queries.md).
