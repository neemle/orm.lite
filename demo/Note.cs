// What this file shows
//   The smallest complete example, the one the README starts with: one class decorated with attributes becomes a table,
//   a generated repository (NoteOrm) and indexes, and one explicit migration seeds a row. Program.cs steps 1 to 4 use
//   it; chapter 10 reads its indexes back from the database.
//
// Library features exercised here
//   [Table("notes")]: the entity is Managed (the default), so the generator writes its CREATE TABLE into the generated
//       baseline migration and the table exists after db.MigrateAsync(). Compare Tracker/Entities.cs, whose tables are
//       Managed = false and are created by hand-written migrations instead.
//   [Key]: the primary key (a Guid the program assigns).
//   [Index] on the class and on a property: the generator writes the indexes into a generated migration, and
//       reconciles them if a declaration changes later. Three indexes result:
//         - unique on title: [Index("title", Unique = true)], ix_notes_title by default (ix_<table>_<columns>)
//         - composite on (stars DESC, title): the term "stars desc" is a property name plus a direction, written as
//           nameof(Stars) + " desc" so the property name is still checked by the compiler; named explicitly
//           ix_notes_stars_title
//         - on stars alone, descending: [Index(Descending = true)] on the property, ix_notes_stars by default
//           (ix_<table>_<columns>)
//       Directions are Native on both PostgreSQL and SQLite, so no capability is needed for them.
//   [Migration] with typed InsertData: a hand-written migration that inserts an OBJECT (a Note) rather than column
//       names and values, using the generated descriptor and Values projection of the entity.
//   The generated NoteOrm (namespace Neemle.Orm.Lite.Generated): Table, Descriptor, Map, Values, Column, Query,
//       InsertAsync, UpdateAsync, DeleteAsync and GetAsync, each insert/update/delete/get also with a connection
//       parameter for use inside a UnitOfWork.

using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Generated;

namespace OrmLiteDemo;

// A free-standing note. Table "notes"; columns id, title, body, stars (snake_case of the property names).
[Table("notes")]
// Titles are unique across the table. Program.cs gives each note it inserts a title with a fresh GUID in it so that a
// second run against a database that keeps its data never trips over this index.
[Index("title", Unique = true)]
// Composite index: stars descending, then title ascending.
[Index(nameof(Stars) + " desc", nameof(Title), Name = "ix_notes_stars_title")]
public sealed class Note
{
    [Key] public Guid Id { get; set; }

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    // An index on this one property, in descending order.
    [Index(Descending = true)] public int Stars { get; set; }
}

// Version 0100, between the generated baseline (0000_..., creates the tables) and the generated reconciliations
// (9900_..., finish a run), so the notes table exists when this runs. It seeds one row, "welcome", as an object; Stars is
// left at its C# default, 0. Program.cs step 2 reads the row back.
// The migration runs once per database: its version is then recorded in schema_migrations and skipped on later runs.
// Down is written out (InsertData has no automatic inverse) and deletes the row by id.
[Migration("0100_welcome_note", Reversibility.Reversible)]
public sealed class WelcomeNote : Migration
{
    public const string Id = "00000000-0000-0000-0000-00000000d3a0";

    public override void Up(MigrationBuilder b) => b.InsertData(
        NoteOrm.Descriptor,
        NoteOrm.Values,
        new Note { Id = Guid.Parse(Id), Title = "welcome", Body = "added by an explicit migration" });

    public override void Down(MigrationBuilder b) => b.DeleteData("notes", "id", Id);
}
