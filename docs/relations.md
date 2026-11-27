# Relations

[Documentation](README.md)

ORM.lite generates methods for loading related entities on SQLite and PostgreSQL. This guide creates an author,
a book, and a tag, then loads the book’s author, the author’s books, and the author’s tags.
For several roots and nested reads, [request an explicit graph](#5-load-an-explicit-graph) with generated typed
paths and finite limits. The individual loaders and manual migration tutorial below remain available.
Navigation declarations alone keep their loading behavior. Database foreign keys are an explicit choice:
use opt-in attributes for a fresh generated baseline, or a migration for an existing database.
For atomic related writes, [save an explicit related graph](#7-save-an-explicit-related-graph): navigation assignment
alone still does not enable persistence.

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

## 3. Choose how to create foreign keys

### Opt in for a fresh baseline

`[ForeignKey(name, onDelete)]` adds a named constraint to the generated baseline. The delete action is required;
there is no implicit action or `ON UPDATE` option. Put the attribute on a reference navigation or its inverse
collection. Both describe the same physical foreign key on the child table.

These two model files are reused from the compiled NativeAOT consumer. They form a separate example from the
author/book/tag tutorial below.

`NativeParent.cs`:

```csharp
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysAot;

[Table("aot_parents")]
public sealed class NativeParent
{
    public Guid Id { get; set; }

    [ForeignKey("fk_aot_child_parent", ReferentialAction.SetNull)]
    public List<NativeChild> Children { get; set; } = [];
}
```

`NativeChild.cs`:

```csharp
using Neemle.OrmLite;

namespace ForeignKeysAot;

[Table("aot_children")]
public sealed class NativeChild
{
    public Guid Id { get; set; }

    public Guid? NativeParentId { get; set; }

    public NativeParent? NativeParent { get; set; }
}
```

The constraint is on `aot_children.native_parent_id`, referencing `aot_parents.id`. A physical parent delete
sets the nullable child key to `null`. Declaring the same name, columns and action on both navigation ends
produces one constraint; conflicting declarations are refused.

| `ReferentialAction` | Effect of a physical delete of the referenced row |
| --- | --- |
| `NoAction` | The database rejects a delete that violates the constraint. |
| `Restrict` | The database restricts deletion while referencing rows remain. |
| `Cascade` | The database deletes the referencing rows. |
| `SetNull` | The database clears the referencing key; that key must be nullable. |

Soft delete updates a row and does not invoke `ON DELETE`. Setting a navigation property also does not save
its entities: call the generated insert/update methods for each row, or explicitly opt in to
[an atomic graph save](#7-save-an-explicit-related-graph).

### Runnable foreign-key cases

The [public demo's prepare/JIT instructions](../examples/foreign-keys/README.md#prepare-and-run) and
[NativeAOT instructions](../examples/foreign-keys/README.md#nativeaot) run one application on SQLite and
PostgreSQL using private packages from this checkout.

- [Parent.cs](../examples/foreign-keys/Parent.cs) and
  [CatalogCases.cs](../examples/foreign-keys/CatalogCases.cs): explicit reference/inverse/link intent,
  quoted names and exact catalog ownership/actions.
- [GeneratedCases.cs](../examples/foreign-keys/GeneratedCases.cs) and
  [LinkCases.cs](../examples/foreign-keys/LinkCases.cs): generated writes and reads, nullable/orphan behavior,
  `CalculatedLower` on an FK-declared model, and calculated values through typed linked reads.
- [ActionCases.cs](../examples/foreign-keys/ActionCases.cs) and
  [SoftDeleteCases.cs](../examples/foreign-keys/SoftDeleteCases.cs): physical actions, soft-delete behavior
  and absence of implicit graph saves.

### Supported relation shapes

These rules apply on both PostgreSQL and SQLite; SQLite must also have enforcement enabled on the connection.
Ordinary entity mapping restrictions still apply; this table does not extend support for keyless or composite models.

| Shape | Baseline opt-in support |
| --- | --- |
| Reference or inverse collection | Managed, non-view endpoints in the same `OrmSchema` contribution; mapped child FK column referencing the target's sole nonnullable primary key. |
| Key types | Child FK and target key must match after removing CLR nullability, including column type, precision and scale. The unrelated primary key of the child is not the referenced target key. |
| Generated pure many-to-many link | Uniquely owned join and same-baseline managed endpoints with `Guid` keys; each end has its own declaration and action. |
| Explicit mapped link entity | Declare ordinary `[ForeignKey]` relations on the link entity; `[LinkForeignKey]` does not apply to an entity-owned join. |
| Self-reference, directed cycle, cross-baseline, unmanaged or view endpoint | Refused. Composite target keys and unsupported link shapes are also refused. |

For a pure managed link, the following annotation excerpt from the compiled generator fixture belongs on
`User.Roles`. Import `Neemle.OrmLite` and `Neemle.OrmLite.Nodes`; both `User.Id` and `Role.Id` are `Guid`.

```csharp
[ManyToMany("user_roles", "user_id", "role_id")]
[LinkForeignKey(LinkForeignKeyEnd.Source, "fk_user", ReferentialAction.Restrict)]
[LinkForeignKey(LinkForeignKeyEnd.Target, "fk_role", ReferentialAction.Cascade)]
public System.Collections.Generic.List<Role> Roles { get; set; } = new();
```

`Source` references the declaring `User`; `Target` references `Role`. This declaration restricts deleting a
linked user and removes join rows when a role is physically deleted. It does not delete the other endpoint.
Generated join columns are nonnullable, so `SetNull` is refused for either end.

Constraint names must be nonempty and contain no NUL, control characters or malformed UTF-16. Accepted
punctuation is quoted as identifier data. Names must be unique, ignoring case, across distinct physical
constraints; the database provider still owns its identifier length limit.

An invalid declaration reports generator diagnostic `ORMLITE035` and withholds the whole invalid baseline.
Manual descriptor planning throws `ForeignKeyDeclarationException`; inspect its `Error`, `Entity` and
`Navigation` properties. Errors distinguish invalid names, missing relations or columns, incompatible or
unsupported keys, unmanaged or external endpoints, duplicate names, conflicting declarations, invalid delete
actions, nonnullable `SetNull`, unsupported links and dependency cycles. Fix the declaration rather than
expecting a partial schema. Tables in a valid opt-in baseline are ordered parent before child.

### Keep the explicit migration route

The author/book/tag models above intentionally have no foreign-key opt-in attributes. Their loading methods
continue to work, and database enforcement comes from the constraints below. This manual route remains
available for loader-only models and existing databases. Do not add the same constraints through both an
opt-in baseline and this migration.
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
Adding an attribute after a baseline was recorded does not retrofit the database; follow the
[existing-database upgrade guidance](migrations.md#foreign-keys-fresh-baseline-and-existing-databases).

### Check SQLite enforcement

The SQLite factory supplies `Foreign Keys=True` when the connection string does not specify the option.
An explicit `Foreign Keys=False` is honored: constraints can exist in the catalog while orphan writes remain
unenforced. Check `PRAGMA foreign_keys;` on the connection that performs the writes: `1` means enabled and `0`
means disabled. A catalog listing alone does not prove enforcement. PostgreSQL enforces an installed foreign
key independently of this SQLite option.

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

Model policies and `RelationReadOptions` apply to supported buffered full-entity reads:
`ToListAsync`, `FirstOrDefaultAsync`, and generated `GetAsync` and navigation loaders.
`EagerBehavior.Suppress` changes automatic eager work for one invocation only; it does not change the
model, later reads, explicit requests or lazy eligibility. See the
[supported-read boundary](../examples/relation-loading/README.md#supported-reads-and-ordinary-streaming).

Ordinary `StreamAsync` follows the [explicit row-stream contract](streaming.md#consume-rows-with-await-foreach),
including on a model with relation policies. It has no policy-aware `RelationReadOptions`, eager-expansion
or retained-scope attachment contract. Choose the supported buffered reads above when using those policies.

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

## 6. Declare and load one-to-one relations

A one-to-one pair has one dependent that owns a physical foreign key and one matching inverse single navigation.
The runnable [one-to-one demo](../examples/one-to-one/README.md) uses an integer-keyed
[Person](../examples/one-to-one/Person.cs) and a Guid-keyed [Profile](../examples/one-to-one/Profile.cs).
Profile.PrincipalId is the nullable integer FK; the dependent's own Guid key does not select the principal.

These navigation declarations are excerpts from those models, in namespace OneToOneDemo:

```csharp
// Profile: owns the mapped int? PrincipalId column.
[OneToOne(nameof(PrincipalId), nameof(global::OneToOneDemo.Person.Profile))]
[ForeignKey("fk_oto_profile_person", ReferentialAction.Restrict)]
[OneToOneUnique("uq_oto_profile_person")]
public Person? Person { get; set; }

// Person: refers back to that exact owner navigation.
[InverseOneToOne(nameof(global::OneToOneDemo.Profile.Person))]
public Profile? Profile { get; set; }
```

Use Neemle.OrmLite and Neemle.OrmLite.Nodes for these attributes and ReferentialAction. Both endpoints must be
managed, non-view tables in the same schema contribution, with supported scalar single primary keys. The mapped
physical FK must match the principal key's type/column shape. Navigation properties have public non-init setters
and stay nullable, including when the physical FK is required: unloaded or visibility-filtered absence is possible.
Composite/alternate keys, self/cyclic pairs, external endpoints and ambiguous or dual ownership are unsupported.

### Load either direction explicitly

[DirectionCases.cs](../examples/one-to-one/DirectionCases.cs) reads the source through its generated repository,
then awaits ProfileOrm.LoadPersonAsync(db, connection, profile) or
PersonOrm.LoadProfileAsync(db, connection, person). Both also accept an existing UnitOfWork instead of the connection.
A normal nonnull demand issues one statement and loads only that navigation, including mapped calculated target fields.
Reading or assigning a navigation property performs no I/O, graph save, eager/lazy traversal or implicit cascade.

A permitted optional null FK clears its navigation without a lookup. An inverse with no dependent returns null;
a required FK does not require every principal to have a dependent. The
[required/null and integrity cases](../examples/one-to-one/IntegrityCases.cs) demonstrate multiple unrelated NULL
FKs and atomic rejection when reassigning to an already occupied principal. The
[required-FK case](../examples/one-to-one/ActionCases.cs) loads a valid nonnullable reference.

### Physical integrity and explicit actions

A fresh baseline installs one named FK and one plain, unfiltered unique index over exactly its owning FK column.
A composite, expression or live-only unique index does not provide this guarantee. The
[catalog case](../examples/one-to-one/CatalogCases.cs) and
[integrity cases](../examples/one-to-one/IntegrityCases.cs) check actual catalog shape, valid writes, orphan refusal
and rejection of a second dependent for one nonnull principal. SQLite enforcement must be enabled on the connection.

The action is explicit: NoAction, Restrict, Cascade, or SetNull for a nullable FK. The normal pair above uses Restrict.
[ActionCases.cs](../examples/one-to-one/ActionCases.cs) exercises physical Cascade/SetNull and NoAction/Restrict
refusal; deleting a dependent leaves its principal. A soft-delete update does not execute a physical ON DELETE action.
Existing databases need the [separate upgrade route](migrations.md#one-to-one-integrity-in-fresh-and-existing-databases).

### Source visibility, duplicates and failure

Generated loaders first require an exact registered generated full-row read with known unmasked provenance.
New objects, raw/custom mapper calls and projections are unknown; any active mask, including a conditional mask,
refuses traversal. OneToOneReadGuard.Require only checks/refuses: calling it directly or repeatedly cannot grant trust.
This check precedes cancellation handling, the optional-null shortcut, SQL and navigation assignment.
[GuardCases.cs](../examples/one-to-one/GuardCases.cs) and
[AdditionalGuardCases.cs](../examples/one-to-one/AdditionalGuardCases.cs) show those zero-SQL refusals.

An owner-to-principal load retains the target's live-row filter and may return null without an unrestricted
existence probe. An inverse into a filtered/soft-delete dependent is unsupported and refuses before SQL;
[VisibilityCases.cs](../examples/one-to-one/VisibilityCases.cs) demonstrates both outcomes. A visible-row count
does not prove physical integrity.

For a permitted plain inverse, the loader reads at most two rows and stages the result before assignment.
Two legacy dependents raise OneToOneCardinalityException rather than choosing a first row; the old navigation
remains unchanged. See [LegacyCases.cs](../examples/one-to-one/LegacyCases.cs), which changes only its disposable fixture.

The caller owns the supplied connection or UnitOfWork, transaction and disposal. The load owns its command/reader,
adds no replacement connection, hidden transaction or retry, and publishes no navigation on a lookup, mapping,
cancellation or cleanup failure. [CancellationCases.cs](../examples/one-to-one/CancellationCases.cs) and
[FailureCases.cs](../examples/one-to-one/FailureCases.cs) show retained caller resources, the original mapper/provider
failure, independent cleanup attempts and cleanup-only refusal.

Use the demo's [package preparation and JIT commands](../examples/one-to-one/README.md#prepare-and-run),
[provider selection](../examples/one-to-one/README.md#choose-the-provider) and
[NativeAOT/.NET 11 commands](../examples/one-to-one/README.md#nativeaot) for the same public cases.

## 7. Save an explicit related graph

`GraphSave.SaveAsync(context, request, cancellationToken)` saves a root and only the supplied related mutations
selected by the request. It owns one connection and transaction for all entity writes, anchor checks and link additions.
Navigation attributes and populated properties do not opt in by themselves.

The [public GraphSave guide](../demo/GraphSave/README.md) contains the
[package preparation and JIT/NativeAOT commands](../demo/GraphSave/README.md#run-with-jit-or-nativeaot).
Its [entry point](../demo/GraphSave/GraphSaveDemo.cs), selected by [Program.cs](../demo/Program.cs), runs the same
cases on PostgreSQL and SQLite with .NET 10 or 11. Use the current checkout's packed packages for this example.

### Declare intent and allowed edges

The sample models are a [project](../demo/GraphSave/GraphSaveProject.cs),
[team](../demo/GraphSave/GraphSaveTeam.cs), [task](../demo/GraphSave/GraphSaveTask.cs) and
[tag](../demo/GraphSave/GraphSaveTag.cs). The [fixture](../demo/GraphSave/GraphSaveFixture.cs) supplies their keys
and navigation objects. `GraphSaveRequest<T>` supplies the root and its explicit root intent; `GraphSavePlan`
supplies related intents, permissions, limits and `Owned` transaction mode.

| Intent | Meaning |
| --- | --- |
| `Insert` | Insert this caller-keyed row. A duplicate Insert may fail; it never turns into an update. |
| `Update` | Write this complete admitted physical payload to an existing row. A missing row fails instead of being inserted. |
| `Unchanged` | Do not mutate this entity. When used as an enabled relation/link anchor, check that its key exists. |

Keys never infer intent. Every reached related instance needs an explicit entry in `GraphSaveIntents`, which uses
object reference identity. Typed generated `SaveRelations` tokens select an exact navigation; `Allow` separately
permits `Insert`, `Update` and, for supported pure links, `AddLink`. Both intents and permissions return immutable
copies. `GraphSavePermissions.None` skips all related getters and writes, even for populated navigations; the
[default-off case](../demo/GraphSave/GraphSaveDefaultCase.cs) checks this against actual rows.

This is the exact permission/intent/save excerpt from
[GraphSaveInsertCase](../demo/GraphSave/GraphSaveInsertCase.cs). Here `db` is its open context and `f` is the
prepared fixture; the [request helper](../demo/GraphSave/GraphSaveChecks.cs) supplies the root request and limits:

```csharp
var permissions = GraphSavePermissions.None
    .Allow(GraphSaveProjectOrm.SaveRelations.Team,
        new(GraphSaveActions.Insert, GraphSaveKeyBinding.BindInWriteBuffer))
    .Allow(GraphSaveProjectOrm.SaveRelations.Tasks,
        new(GraphSaveActions.Insert, GraphSaveKeyBinding.BindInWriteBuffer))
    .Allow(GraphSaveProjectOrm.SaveRelations.Tags, new(GraphSaveActions.Insert | GraphSaveActions.AddLink));
var intents = GraphSaveIntents.Empty.With(f.Team, GraphSaveIntent.Insert).With(f.First, GraphSaveIntent.Insert)
    .With(f.Sibling, GraphSaveIntent.Insert).With(f.Tag, GraphSaveIntent.Insert);
var result = await GraphSave.SaveAsync(db, GraphSaveChecks.Request(f.Project, GraphSaveIntent.Insert,
    intents, permissions));
```

`RequireMatch` requires an FK to match the related key already. `BindInWriteBuffer` explicitly binds only the
relation-owned FK in the detached write buffer; input FK properties stay unchanged. After an acknowledged commit,
the fixture [explicitly adopts result bindings](../demo/GraphSave/GraphSaveFixture.cs) through
`BoundForeignKey<TEntity,TKey>` before its subsequent updates. The library never changes keys, FKs, navigations
or intents and does not mark an object as persisted.

The [repeat case](../demo/GraphSave/GraphSaveRepeatCase.cs) uses `Unchanged` team/tag anchors and explicit updates.
An enabled edge to an Unchanged anchor still needs a rule, which may allow `GraphSaveActions.None`;
that rule does not license an entity write. Repeating the permitted Guid-key link adds no duplicate exact pair.
Omitted children and links survive; omission never means delete, soft delete, orphan removal, unlink or FK clearing.

### Bound work and supported shapes

Every request supplies all six positive finite [traversal and work limits](../demo/GraphSave/README.md#limits-and-database-work).
They count depth, encountered instances, examined allowed slots, unique entity writes, unique link pairs and
planned write/link/anchor statements. Exceeded bounds fail before a transaction rather than save a prefix.
Shared identities require matching intent and final physical/FK values; conflicts and remaining mutation cycles refuse.
Only supplied safe auto-properties and concrete Lists are traversed; missing or disabled navigations trigger no discovery.

Supported mappings are generated managed non-view entities with one supported caller-supplied scalar key, ordinary
references/collections and pure managed Guid-key links with the known pair key. Unsupported generated/composite keys,
custom/payload joins, unsafe getters/custom collections and policy-specialized mappings refuse. Known projected or
masked partial input refuses before a transaction, with no hidden full-row reread or promotion. Manual objects remain
explicit host-authorized full-payload requests; missing provenance is not authorization or proof of completeness.

### Ownership, rollback and uncertain outcomes

`Owned` is the supported transaction mode. `CallerJoined`, bare connections and caller-owned transaction/UoW
participation are unsupported; no savepoint, hidden join or caller-commit safety is inferred.

| Outcome | What the caller knows |
| --- | --- |
| `NotStarted` | Admission failed or was canceled before the transaction; no graph write occurred. |
| `RolledBack` | A precommit failure had an acknowledged rollback; no new subset remains committed. |
| `Committed` | Commit was acknowledged. Later cleanup failure can throw with `CommittedResult`; it does not undo the commit. |
| `Unknown` | Rollback or commit response is unconfirmed. Reconcile with domain keys before deciding whether to retry. |

The [rollback case](../demo/GraphSave/GraphSaveRollbackCase.cs) performs two successful writes before a third
constraint conflict, then checks that no new prefix survived and that the conflicting existing row and input graph
remain intact. The [refusal case](../demo/GraphSave/GraphSaveRefusalCase.cs) exercises action restrictions, a missing
Unchanged anchor, an enforced physical FK, known partial input and pre-admission cancellation.

Precommit cancellation after transaction admission initiates rollback independently of the canceled token. Once commit
starts, await its actual outcome; late cancellation cannot establish rollback. There is no automatic retry, implicit upsert
or stronger isolation promise. Caller edits are not rolled back or tracked; serialize edits during capture.

Safe error/outcome formatting does not expose row values or raw native details. `InnerException` remains sensitive
programmatic diagnostic data; avoid automatically serializing it. The public demo prints safe case results and checks
the same insert/update/default-off/repeat/rollback/refusal assertions in all eight JIT/NativeAOT × provider × runtime
combinations. It uses a new scratch database/file per invocation and requires successful cleanup before reporting success.
