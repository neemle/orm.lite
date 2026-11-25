# Relations

[Documentation](README.md)

ORM.lite generates methods for loading related entities on SQLite and PostgreSQL. This guide creates an author,
a book, and a tag, then loads the book’s author, the author’s books, and the author’s tags.

| Relation | Model declaration | Generated methods |
| --- | --- | --- |
| Many-to-one | `Book.AuthorId` and `Book.Author` | `BookOrm.LoadAuthorAsync` |
| One-to-many | `Author.Books` | `AuthorOrm.LoadBooksAsync` |
| Many-to-many | `[ManyToMany]` on `Author.Tags` | `AuthorOrm.AddTagAsync`, `RemoveTagAsync`, `LoadTagsAsync` |

## 1. Create the sample project

From the repository root, build the packages and configure a temporary console project. See
[Installation](installation.md) for SDK and provider requirements.

```bash
dotnet pack Neemle.OrmLite.slnx -c Release -o artifacts -m:1
ormlite_feed="$PWD/artifacts"
ormlite_sample="$(mktemp -d)"
dotnet new console -n RelationsSample -o "$ormlite_sample" -f net10.0
cd "$ormlite_sample"
dotnet new nugetconfig
dotnet nuget add source "$ormlite_feed" --name orm-lite-local --configfile nuget.config
dotnet add package Neemle.OrmLite.Sqlite --prerelease
```

## 2. Declare the relations

Create `Models.cs`.

```csharp
using Neemle.OrmLite;

[Table("authors")]
public sealed class Author
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<Book> Books { get; set; } = new();

    [ManyToMany("author_tags", "author_id", "tag_id")]
    public List<Tag> Tags { get; set; } = new();
}

[Table("books")]
public sealed class Book
{
    [Key] public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public Author? Author { get; set; }
}

[Table("tags")]
public sealed class Tag
{
    [Key] public Guid Id { get; set; }
    public string Label { get; set; } = "";
}
```

- **Book → Author:** The generator looks for a key property named after the navigation, here `AuthorId`.
- **Author → Books:** The inverse collection looks for a property named after the parent type, here `AuthorId`
  on `Book`. To name the property explicitly, add `[Reference(nameof(Book.AuthorId))]` to `Author.Books` or
  `Book.Author`. The generator uses the property named by the attribute.
- **Author ↔ Tags:** The attribute names the join table, then its author and tag columns. For this simple
  managed link, the baseline creates `author_tags` with a primary key over both columns.

## 3. Add foreign keys in a migration

The navigation declarations generate loading methods. Database enforcement comes from the constraints below.
Create `RelationsForeignKeys.cs` to add a foreign key for the book and one for each side of the join table.

```csharp
using Neemle.OrmLite;

[Migration("0100_relations_foreign_keys", Reversibility.Reversible)]
public sealed class RelationsForeignKeys : Migration
{
    public override void Up(MigrationBuilder b) => b
        .AddForeignKey("fk_books_author", "books", "author_id", "authors", "id")
        .AddForeignKey("fk_author_tags_author", "author_tags", "author_id", "authors", "id")
        .AddForeignKey("fk_author_tags_tag", "author_tags", "tag_id", "tags", "id");

    public override void Down(MigrationBuilder b) => b
        .DropForeignKey("fk_author_tags_tag", "author_tags")
        .DropForeignKey("fk_author_tags_author", "author_tags")
        .DropForeignKey("fk_books_author", "books");
}
```

The baseline creates the tables before this migration runs. For an existing database, ensure existing rows
satisfy the constraints before applying the migration. For an explicit delete action such as `ON DELETE CASCADE`,
see the [tracker migration](../demo/Tracker/Migrations.cs) and the [migration guide](migrations.md).

## 4. Insert, load, and unlink

Replace `Program.cs`.

```csharp
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

var connectionString = Environment.GetEnvironmentVariable("ORMLITE_RELATIONS_POSTGRES");
var options = string.IsNullOrWhiteSpace(connectionString)
    ? new OrmLiteOptions { Provider = "sqlite", Path = "relations.db" }
    : new OrmLiteOptions { Provider = "postgres", ConnectionString = connectionString };

await using var db = await OrmLite.OpenAsync(options);
await db.MigrateAsync();

var author = new Author { Id = Guid.NewGuid(), Name = "Ursula Le Guin" };
var book = new Book { Id = Guid.NewGuid(), AuthorId = author.Id, Title = "A Wizard of Earthsea" };
var tag = new Tag { Id = Guid.NewGuid(), Label = "Fantasy" };

await AuthorOrm.InsertAsync(db, author);
await BookOrm.InsertAsync(db, book);
await TagOrm.InsertAsync(db, tag);

await using var connection = await db.DataSource.OpenConnectionAsync();
await BookOrm.LoadAuthorAsync(db, connection, book);
await AuthorOrm.LoadBooksAsync(db, author);
Console.WriteLine($"Author: {book.Author!.Name}");
Console.WriteLine($"Book: {author.Books.Single().Title}");

await AuthorOrm.AddTagAsync(db, connection, author.Id, tag.Id);
await AuthorOrm.LoadTagsAsync(db, connection, author);
Console.WriteLine($"Tag: {author.Tags.Single().Label}");

await AuthorOrm.RemoveTagAsync(db, connection, author.Id, tag.Id);
await AuthorOrm.LoadTagsAsync(db, connection, author);
Console.WriteLine($"Links left: {author.Tags.Count}");
```

Run `dotnet run`. Expected output:

```text
Author: Ursula Le Guin
Book: A Wizard of Earthsea
Tag: Fantasy
Links left: 0
```

The load methods populate navigation properties explicitly. `RemoveTagAsync` deletes the link row and keeps the
tag. Use a [unit of work](../demo/README.md#what-each-chapter-proves) when several writes must commit together.

### Run on PostgreSQL

Add the PostgreSQL provider package and set `ORMLITE_RELATIONS_POSTGRES` to the connection string of an empty
PostgreSQL database. For a local server that allows passwordless TCP access for your user, this example
creates a database and runs the same code.

```bash
createdb -h localhost -U "$(id -un)" ormlite_relations
dotnet add package Neemle.OrmLite.Postgres --prerelease
ORMLITE_RELATIONS_POSTGRES="Host=localhost;Database=ormlite_relations;Username=$(id -un)" dotnet run
```
