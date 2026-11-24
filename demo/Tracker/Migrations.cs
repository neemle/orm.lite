// What this file shows
//   How a schema CHANGES over time. The nine [Migration] classes below are the history of the tracker tables, from
//   an empty database to the shape that Entities.cs describes. db.MigrateAsync() (Program.cs) applies every version
//   that schema_migrations has not recorded yet, in ordinal order of the version string, each in its own
//   transaction. A database that has run some of them applies only the rest.
//
// Library features exercised here
//   [Migration(version, Reversibility)]: a hand-written migration. The generator registers it at build time, so
//       nothing names these classes in code. Reversibility.Reversible means the migration can be undone (MigrationRunner
//       .RevertAsync runs its Down); Irreversible means it cannot.
//   MigrationBuilder: the typed operations (CreateTable, AddColumn, RenameColumn, CreateIndex, InsertData, ...). Each
//       call is recorded as a node tree, not as SQL text; the provider (PostgreSQL or SQLite) serialises it, so the same
//       class runs on both. No SQL appears in this file.
//   Down: when every operation in Up has an inverse, the library derives Down by itself (CreateTable <-> DropTable,
//       AddColumn <-> DropColumn, RenameColumn back, CreateIndex <-> DropIndex, AddForeignKey <-> DropForeignKey,
//       CreateView <-> DropView). InsertData and anything added through Add(node) have no derived inverse, so those
//       migrations write their Down out. Dropping a column or table cannot be undone without losing data, so such a
//       migration is declared Irreversible and has no Down. The generator warns (ORMLITE017) when the declaration
//       and the operations disagree.
//   Capability, Native / Emulated / None: some of these operations are Native on PostgreSQL and Emulated on SQLite
//       (for example AddForeignKey, capability AlterConstraint: SQLite cannot add a foreign key to an existing table,
//       so the provider rebuilds the table and runs foreign_key_check before committing). Emulated means the same
//       meaning, done a different way; the migration text does not change.
//
// Why the versions run in this order
//   0200  create the four tables in their FIRST shape (no region, no settings, a column still called legacy_code)
//   0210  write rows against that first shape
//   0220  add columns; the rows from 0210 receive the column defaults
//   0230  rename a column; its data follows
//   0240  add foreign keys onto tables that already hold rows
//   0250  add indexes, including one on the renamed column (which is why 0230 comes first)
//   0260  drop an index and a column
//   0270  seed rows as objects (which needs 0220: the objects have the columns 0220 added)
//   0280  create two views over the finished tables
//   The versions are plain strings compared ordinally. The generated baseline is 0000_..., the generated index
//   migration 0001_..., the reconciliations the generator appends start with 9900_, so 0100_welcome_note (Note.cs)
//   and these 02xx versions run after the baseline and before the reconciliations.
//   Chapter 8 (Chapters.cs) reads the final result back from the database catalog.

using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Generated;
using Neemle.Orm.Lite.Nodes;

namespace OrmLiteDemo;

// Fixed identifiers for the rows the migrations write, so that later chapters can find the same rows on every run.
// The prefix tells the table: ...e00xx teams, ...f00xx projects, ...1000x work items, ...2000x comments.
internal static class Ids
{
    public static readonly Guid Platform = Guid.Parse("00000000-0000-0000-0000-0000000e0001");
    public static readonly Guid Growth = Guid.Parse("00000000-0000-0000-0000-0000000e0002");
    public static readonly Guid Core = Guid.Parse("00000000-0000-0000-0000-0000000f0001");
    public static readonly Guid Billing = Guid.Parse("00000000-0000-0000-0000-0000000f0002");
    public static readonly Guid Api = Guid.Parse("00000000-0000-0000-0000-000000010001");
    public static readonly Guid Queue = Guid.Parse("00000000-0000-0000-0000-000000010002");
    public static readonly Guid Invoice = Guid.Parse("00000000-0000-0000-0000-000000010003");
    public static readonly Guid Refund = Guid.Parse("00000000-0000-0000-0000-000000010004");
    public static readonly Guid ApiNote = Guid.Parse("00000000-0000-0000-0000-000000020001");
    public static readonly Guid InvoiceNote = Guid.Parse("00000000-0000-0000-0000-000000020002");
    public static readonly Guid RefundNote = Guid.Parse("00000000-0000-0000-0000-000000020003");
}

// Two small helpers for node-level DDL. MigrationBuilder.AddForeignKey(...) has no argument for the referential action,
// so a foreign key with ON DELETE CASCADE is built as the AddForeignKey node itself and handed to b.Add(node).
internal static class Ddl
{
    public static NodeList Columns(params string[] names) => NodeList.Of(names.Select(n => (Node)new ColumnRef(n)));

    // A foreign key named `name` from table.column to parent.id that deletes the child rows when the parent row goes.
    public static AddForeignKey Cascade(string name, string table, string column, string parent) =>
        new(name, table, Columns(column), parent, Columns("id"), ReferentialAction.Cascade);
}

// 0200: the first shape of the four tables. Column(name, type, nullable, primaryKey, default): the id column is
// "not nullable, primary key". legacy_code is nullable (no flag) and will be renamed in 0230. work_items carries
// column defaults (Defaults.Number, Defaults.Text, Defaults.Now), which the database applies to a row written without
// those columns. There are no foreign keys yet; 0240 adds them once the tables hold rows.
// No Down is written: CreateTable has an inverse (DropTable), so the library derives it.
[Migration("0200_tracker_tables", Reversibility.Reversible)]
public sealed class TrackerTables : Migration
{
    public override void Up(MigrationBuilder b) => b
        .CreateTable("teams", t => t
            .Column("id", ColumnType.Uuid, false, true)
            .Column("name", ColumnType.Text, false))
        .CreateTable("projects", t => t
            .Column("id", ColumnType.Uuid, false, true)
            .Column("team_id", ColumnType.Uuid, false)
            .Column("title", ColumnType.Text, false)
            .Column("legacy_code", ColumnType.Text))
        .CreateTable("work_items", t => t
            .Column("id", ColumnType.Uuid, false, true)
            .Column("project_id", ColumnType.Uuid, false)
            .Column("title", ColumnType.Text, false)
            .Column("points", ColumnType.Int32, false, false, Defaults.Number(0))
            .Column("status", ColumnType.Text, false, false, Defaults.Text("open"))
            .Column("created_at", ColumnType.Timestamp, false, false, Defaults.Now))
        .CreateTable("comments", t => t
            .Column("id", ColumnType.Uuid, false, true)
            .Column("work_item_id", ColumnType.Uuid, false)
            .Column("body", ColumnType.Text, false));
}

// 0210: the first rows, written while the tables still have their first shape. The Team and Project classes already
// have properties for columns that do not exist yet (Region, Settings), so this migration cannot insert objects; it
// names the columns and gives the values (the column-array form of InsertData). The rows are a team "platform" and
// its project "core" with legacy_code "C-1".
// InsertData has no automatic inverse, so Down is written by hand: DeleteData removes by column value, children
// before parents.
[Migration("0210_tracker_first_rows", Reversibility.Reversible)]
public sealed class TrackerFirstRows : Migration
{
    public override void Up(MigrationBuilder b) => b
        .InsertData("teams", ["id", "name"], [Ids.Platform.ToString(), "platform"])
        .InsertData("projects", ["id", "team_id", "title", "legacy_code"], [Ids.Core.ToString(), Ids.Platform.ToString(), "core", "C-1"]);

    public override void Down(MigrationBuilder b) => b
        .DeleteData("projects", "id", Ids.Core.ToString())
        .DeleteData("teams", "id", Ids.Platform.ToString());
}

// 0220: new columns on tables that already hold the rows of 0210. teams.region is not nullable and has the default
// 'eu', so the existing team receives 'eu'; projects.settings is a not-nullable JSON column whose default is the empty
// document {}, so the existing project receives {}. Chapter 8 reads both values back to prove the old rows were kept
// and filled in. Both operations have inverses (DropColumn), so Down is derived.
[Migration("0220_tracker_new_columns", Reversibility.Reversible)]
public sealed class TrackerNewColumns : Migration
{
    public override void Up(MigrationBuilder b) => b
        .AddColumn("teams", "region", ColumnType.Text, false, Defaults.Text("eu"))
        .AddJsonbColumn("projects", "settings", false);
}

// 0230: rename projects.legacy_code to code. The values stay, and so would any index on the column. It has to run
// before 0250, which creates an index on "code". The inverse (a rename back) is derived.
[Migration("0230_tracker_rename_code", Reversibility.Reversible)]
public sealed class TrackerRenameCode : Migration
{
    public override void Up(MigrationBuilder b) => b.RenameColumn("projects", "legacy_code", "code");
}

// 0240: three foreign keys, each ON DELETE CASCADE, added to tables that already contain rows (the rows of 0210
// satisfy them: project "core" belongs to team "platform"). PostgreSQL adds the constraint to the existing table; on
// SQLite the provider rebuilds each table (the capability AlterConstraint is Emulated there) and checks foreign keys
// before it commits.
// The keys are added through Add(node) (see Ddl.Cascade above), which the generator's scan of Up cannot classify,
// so Down is written out: it drops the keys, children first.
[Migration("0240_tracker_foreign_keys", Reversibility.Reversible)]
public sealed class TrackerForeignKeys : Migration
{
    public override void Up(MigrationBuilder b) => b
        .Add(Ddl.Cascade("fk_projects_team", "projects", "team_id", "teams"))
        .Add(Ddl.Cascade("fk_work_items_project", "work_items", "project_id", "projects"))
        .Add(Ddl.Cascade("fk_comments_work_item", "comments", "work_item_id", "work_items"));

    public override void Down(MigrationBuilder b) => b
        .DropForeignKey("fk_comments_work_item", "comments")
        .DropForeignKey("fk_work_items_project", "work_items")
        .DropForeignKey("fk_projects_team", "projects");
}

// 0250: four indexes.
//   ix_work_items_project_points  composite, project_id ascending then points DESCENDING (IndexTerm carries the
//                                 direction; chapter 10 prints how each provider writes it). Serves "the biggest item
//                                 of a project first".
//   ux_projects_team_title        UNIQUE on (team_id, title): one project title per team. Chapter 10 inserts a duplicate
//                                 and the database refuses it.
//   ix_projects_code              plain index on the column renamed in 0230. 0260 will drop the column; this index is
//                                 there to show that dropping a column also removes an index on it.
//   ix_work_items_status          plain index; 0260 drops it by name.
// All four operations have inverses, so Down could be derived; it is written out here to show the reverse order.
[Migration("0250_tracker_indexes", Reversibility.Reversible)]
public sealed class TrackerIndexes : Migration
{
    public override void Up(MigrationBuilder b) => b
        .CreateIndex("ix_work_items_project_points", "work_items", IndexTerm.Asc("project_id"), IndexTerm.Desc("points"))
        .CreateUniqueIndex("ux_projects_team_title", "projects", "team_id", "title")
        .CreateIndex("ix_projects_code", "projects", "code")
        .CreateIndex("ix_work_items_status", "work_items", "status");

    public override void Down(MigrationBuilder b) => b
        .DropIndex("ix_work_items_status")
        .DropIndex("ix_projects_code")
        .DropIndex("ux_projects_team_title")
        .DropIndex("ix_work_items_project_points");
}

// 0260: retire the code. It drops one index by name and one column. Dropping projects.code also has to remove
// ix_projects_code, which is not named here: PostgreSQL drops an index together with its column, and the SQLite
// provider drops the indexes that use the column before it drops the column. Chapter 8 checks that the column and
// the index are both gone.
// Declared Irreversible: the values of the dropped column cannot be restored by a Down, and a Reversible migration with
// DropColumn and no Down would be flagged by ORMLITE017. An Irreversible migration has no Down.
[Migration("0260_tracker_retire_code", Reversibility.Irreversible)]
public sealed class TrackerRetireCode : Migration
{
    public override void Up(MigrationBuilder b) => b
        .DropIndex("ix_work_items_status")
        .DropColumn("projects", "code");
}

// 0270: the seed data, written as OBJECTS. InsertData(TeamOrm.Descriptor, TeamOrm.Values, new Team { ... }) takes the
// generated descriptor of the entity (its table and columns) and the generated Values projection, so the row is
// built with the C# class and its property names, not with column names and positional values. The catch: an object
// has the shape of the entity as it is today, so this migration must run after every migration that adds a column the
// entity has. That is why it is 0270 and not 0210: Team.Region and Project.Settings exist only after 0220.
// Content: a second team "growth" (region us), its project "billing" (with a JSON settings document), four work items
// (two in "core", from 0210; two in "billing") and three comments. Statuses: open, done, open, blocked; points 8, 5,
// 3, 13. Later chapters count on exactly these numbers.
// Timestamps are UTC (DateTimeKind.Utc), the kind the library stores as it is.
// Down is written out because InsertData has no automatic inverse; it deletes what Up inserted, children first.
[Migration("0270_tracker_seed", Reversibility.Reversible)]
public sealed class TrackerSeed : Migration
{
    private static readonly DateTime Start = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

    public override void Up(MigrationBuilder b) => b
        .InsertData(TeamOrm.Descriptor, TeamOrm.Values, new Team { Id = Ids.Growth, Name = "growth", Region = "us" })
        .InsertData(
            ProjectOrm.Descriptor,
            ProjectOrm.Values,
            new Project { Id = Ids.Billing, TeamId = Ids.Growth, Title = "billing", Settings = Jsonb.Parse("{\"owner\":\"bob\",\"wip\":2,\"labels\":[\"finance\"]}") })
        .InsertData(
            WorkItemOrm.Descriptor,
            WorkItemOrm.Values,
            new WorkItem { Id = Ids.Api, ProjectId = Ids.Core, Title = "public api", Points = 8, Status = "open", CreatedAt = Start },
            new WorkItem { Id = Ids.Queue, ProjectId = Ids.Core, Title = "job queue", Points = 5, Status = "done", CreatedAt = Start.AddDays(1) },
            new WorkItem { Id = Ids.Invoice, ProjectId = Ids.Billing, Title = "invoice export", Points = 3, Status = "open", CreatedAt = Start.AddDays(2) },
            new WorkItem { Id = Ids.Refund, ProjectId = Ids.Billing, Title = "refund flow", Points = 13, Status = "blocked", CreatedAt = Start.AddDays(3) })
        .InsertData(
            CommentOrm.Descriptor,
            CommentOrm.Values,
            new Comment { Id = Ids.ApiNote, WorkItemId = Ids.Api, Body = "needs a design" },
            new Comment { Id = Ids.InvoiceNote, WorkItemId = Ids.Invoice, Body = "csv first" },
            new Comment { Id = Ids.RefundNote, WorkItemId = Ids.Refund, Body = "waiting for legal" });

    public override void Down(MigrationBuilder b) => b
        .DeleteData("comments", "id", Ids.ApiNote.ToString(), Ids.InvoiceNote.ToString(), Ids.RefundNote.ToString())
        .DeleteData("work_items", "id", Ids.Api.ToString(), Ids.Queue.ToString(), Ids.Invoice.ToString(), Ids.Refund.ToString())
        .DeleteData("projects", "id", Ids.Billing.ToString())
        .DeleteData("teams", "id", Ids.Growth.ToString());
}
