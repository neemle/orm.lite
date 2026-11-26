# Relations

[Documentation](README.md)

ORM.lite generates methods for loading related entities on SQLite and PostgreSQL. This guide creates an author,
a book, and a tag, then loads the book’s author, the author’s books, and the author’s tags.
For several roots and nested reads, [request an explicit graph](#5-load-an-explicit-graph) with generated typed
paths and finite limits. The individual loaders and manual migration tutorial below remain available.

| Relation | Model declaration | Generated methods |
| --- | --- | --- |
| Many-to-one | `Book.AuthorId` and `Book.Author` | `BookOrm.LoadAuthorAsync` |
| One-to-many | `Author.Books` | `AuthorOrm.LoadBooksAsync` |
| Many-to-many | `[ManyToMany]` on `Author.Tags` | `AuthorOrm.AddTagAsync`, `RemoveTagAsync`, `LoadTagsAsync` |

## 1. Create the sample project

From any directory, create a .NET 10 console app and add the SQLite provider.

```bash
dotnet new console -n RelationsSample -f net10.0
cd RelationsSample
dotnet add package Neemle.OrmLite.Sqlite --version 0.1.0
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
dotnet add package Neemle.OrmLite.Postgres --version 0.1.0
ORMLITE_RELATIONS_POSTGRES="Host=localhost;Database=ormlite_relations;Username=$(id -un)" dotnet run
```

## 5. Load an explicit graph

`ToGraphListAsync(request, cancellationToken)` returns a full-entity root list with the requested navigations
populated. Import `Neemle.OrmLite` for the graph types and extension method, and `Neemle.OrmLite.Generated` for
generated repositories. Use generated `Paths` tokens rather than relation names or SQL strings.

The following declaration excerpt is from the compiled NativeAOT consumer's
[AotGraphAuthor.cs](../tests/AotSmoke/AotGraphAuthor.cs). Its companion
[book](../tests/AotSmoke/AotGraphBook.cs) has a nullable `EditorId` and `Editor` reference back to an author;
the [tag](../tests/AotSmoke/AotGraphTag.cs) uses the [tag key enum](../tests/AotSmoke/AotGraphTagKey.cs).
These are separate fixture models, not additions to the author/book/tag tutorial above. `Managed = false`
means that this fixture creates its tables separately; graph reads do not create or change a schema.

```csharp
[Table("aot_graph_authors", Managed = false)]
internal sealed class AotGraphAuthor
{
    [Key] public int Id { get; set; }
    public List<AotGraphBook> Books { get; set; } = [];
    [ManyToMany("aot_graph_links", "author_id", "tag_id")]
    public List<AotGraphTag> Tags { get; set; } = [];
}
```

This exact call excerpt is reused from [GraphProbe.cs](../tests/AotSmoke/GraphProbe.cs), where `db` is an open,
seeded context. It requests Books, Tags and Books → Editor for every selected author:

```csharp
var request = new GraphRequest<AotGraphAuthor>(
    new(new(3, 3, 4), new(2, 20, 8)), AotGraphAuthorOrm.Paths.Books,
    AotGraphAuthorOrm.Paths.Tags, AotGraphAuthorOrm.Paths.Books.Then(AotGraphBookOrm.Paths.Editor));
var roots = await AotGraphAuthorOrm.Query(db).OrderBy(a => a.Id).ToGraphListAsync(request);
```

The nested construction is `GraphLimits(GraphTraversalLimits, GraphWorkLimits)`. Its example values allow
depth 3, 3 complete paths, 4 prefix nodes, 2 roots, 20 related physical rows and 8 statements. They are fixture
budgets, not recommended production defaults. Choose limits for your application's expected fan-out and work.

`Then` appends one generated edge whose source type matches the preceding target. Only the listed paths are
traversed; there is no implicit eager or lazy traversal. Duplicate paths and shared prefixes are normalized.
The request snapshots its path array, and its limits are immutable.

### Root page, batching and identity

Apply ordinary `Where`, explicit `OrderBy`, `Skip` and `Take` before `ToGraphListAsync`. The root query runs
first, so related rows neither multiply roots nor consume the root page. Root ordering remains the query's
ordering; graph loading does not add a collection ordering contract. An empty root page runs no relation query.

References batch distinct target keys, collections batch inverse foreign keys, and many-to-many reads batch
join associations and target rows. Work is grouped across roots for each requested prefix and key chunk,
rather than one relation query per root. Chunks respect the provider's bind-parameter capacity after reserving
other parameters. One caller-facing operation therefore uses several SQL statements, not one joined statement.

Missing or nullable references remain `null`; collections with no visible members are empty. Shared targets
reuse the same entity instance within the call, keyed by entity type and typed physical key. A nested Editor
may therefore be the same object as another returned root. There is no cross-call identity cache or tracking.

### Supply all six bounds

There is no default or unlimited graph budget. Every invocation supplies strictly positive values for:

| Group | Limit | What counts |
| --- | --- | --- |
| `GraphTraversalLimits` | `MaxDepth` | Edges in the longest requested path. |
| `GraphTraversalLimits` | `MaxPaths` | Distinct complete requested paths after deduplication. |
| `GraphTraversalLimits` | `MaxNodes` | Distinct canonical prefix nodes, excluding the root. |
| `GraphWorkLimits` | `MaxRoots` | Root rows returned by the root query. |
| `GraphWorkLimits` | `MaxRelatedRows` | All physical relation rows read, including link association rows; identity reuse or discarded null links do not refund work. |
| `GraphWorkLimits` | `MaxStatements` | All executed root and relation statements. |

An explicit `Take` greater than `MaxRoots` refuses before I/O. Without an explicit `Take`, root reading
detects overflow rather than silently truncating the result. Related reading likewise stops at an overflow
witness, and the statement cap is checked before another statement executes. These bounds limit work; they
do not establish a latency, memory-byte or snapshot guarantee.

### Visibility and trusted host policy

The ordinary root filter, live-row selection and scalar masks remain effective. Related targets retain the
existing loader live-row restriction. `IncludingDeleted()` on the root does not silently include deleted
related targets. When a nested path revisits the root entity type, it retains that type's root masks and cannot
replace a masked instance with an unrestricted one.

An unconditional or conditional mask on a required identity or traversal key refuses the request before root
I/O. A conditional mask is not bypassed because its condition might be false for the current rows.

Generated registrations are trusted application composition, not an authorization sandbox. Root `Where`
conditions do not become row-authorization or tenant filters on other related tables. The host must authorize
the requested relations and access to every related entity type. This API adds no per-path target policy or
tenant isolation mechanism; custom registrations and their mapping/assignment delegates remain trusted code.

### Refusal, cancellation and resource ownership

The graph API supports registered full-entity queries over the existing mapped reference, collection and link
shapes. Custom projections, keyless or unsupported shapes, invalid paths/bindings, invalid limits and missing
bind capacity are refused before root I/O. Failure types distinguish the cause:

| Failure | Public signal |
| --- | --- |
| Invalid request, unsupported shape or binding/capacity problem | `GraphRequestException.Error` (`GraphRequestError`). |
| A required key is masked | `GraphVisibilityException.Path` and `.Property`. |
| A traversal or work bound is exceeded | `GraphLimitExceededException.Budget` and `.Limit`. |
| Cancellation | `OperationCanceledException`; pass the token as the second argument to `ToGraphListAsync`. |
| Provider, timeout or mapper failure | The existing provider/runtime exception. |

The operation returns the list only after every requested path succeeds. Failure, cancellation or overflow
returns no partial list, and does not silently truncate or retry. This is a result-publication guarantee,
not a database transaction or a guarantee that no work occurred before failure.

With no `On` binding, the operation owns one connection and disposes it, its commands and readers. With
`On(unitOfWork)` or `On(connection, transaction)`, every statement uses that exact caller binding; the caller
still owns commit, rollback and disposal. Commands run sequentially. No hidden transaction, snapshot or retry
is added, so concurrent changes can be visible between statements and a repeated call can return different data.

Graph request and visibility errors carry identifier context rather than row or parameter values. Existing
[SQL diagnostics](diagnostics.md#parameter-values-and-redaction) still apply: parameter logging is off by
default, and enabling it does not make heuristic secret redaction complete. Keep values disabled when that
privacy boundary is required.

## 6. Try model-based eager and asynchronous lazy loading

The [runnable relation-loading demo](../examples/relation-loading/README.md#run) contains commands for
JIT and NativeAOT on SQLite and PostgreSQL. It runs the same scenarios in every mode. Navigation getters
perform no I/O; lazy relations load through an explicit asynchronous demand on a retained `LoadingScope`.

- [Eager reads and per-call suppression](../examples/relation-loading/README.md#eager-reads-and-suppression):
  [ReadCases.cs](../examples/relation-loading/ReadCases.cs).
- [Lazy demand, cached state and detach](../examples/relation-loading/README.md#explicit-lazy-demand):
  [LazyCases.cs](../examples/relation-loading/LazyCases.cs).
- [Cancellation, retry and bounded lifetime](../examples/relation-loading/README.md#cancellation-faults-and-bounded-work):
  [ConcurrencyCases.cs](../examples/relation-loading/ConcurrencyCases.cs) and
  [LifetimeCases.cs](../examples/relation-loading/LifetimeCases.cs).
- [Transactions and connection origins](../examples/relation-loading/README.md#transactions-and-connection-origins):
  [BindingCases.cs](../examples/relation-loading/BindingCases.cs) and
  [GateOriginCases.cs](../examples/relation-loading/GateOriginCases.cs).
- [Visibility and captured keys](../examples/relation-loading/README.md#visibility-and-captured-keys):
  [VisibilityCases.cs](../examples/relation-loading/VisibilityCases.cs).
