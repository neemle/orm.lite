// What this file shows
//   The four entities of the "tracker" domain, as plain classes. Each class describes a table through attributes; the
//   source generator reads them at build time and writes, per entity, a repository class named <Entity>Orm
//   (TeamOrm, ProjectOrm, WorkItemOrm, CommentOrm) in the Neemle.Orm.Lite.Generated namespace. Nothing in this file
//   runs on its own; Migrations.cs creates the tables, Chapters.cs uses the repositories.
//
// Library features exercised here
//   [Table(name, Managed = false)]: the entity is mapped and gets its repository, but the generator does NOT write
//       a CREATE TABLE for it (see "Why Managed = false" below).
//   [Key]: the primary key. The key is a Guid that the program assigns itself; the database does not generate it.
//   Jsonb: the library's JSON document type (a jsonb column on PostgreSQL, a text column checked by json_valid on
//       SQLite; the capability is Native on PostgreSQL and Emulated on SQLite).
//   Naming: with no [Column] the column name is the snake_case of the property (TeamId -> team_id, CreatedAt ->
//       created_at), so the migrations in Migrations.cs spell the same names.
//
// The domain, in plain words
//   A Team owns Projects. A Project holds WorkItems (tasks, with an estimate in points and a status). A WorkItem
//   collects Comments.
//
//       Team  1 ---- *  Project  1 ---- *  WorkItem  1 ---- *  Comment
//       teams           projects           work_items          comments
//             team_id ->        project_id ->        work_item_id ->
//
//   The relation is only a Guid property on the "many" side (Project.TeamId, WorkItem.ProjectId,
//   Comment.WorkItemId); there are no navigation properties. The database enforces the relation with three
//   foreign keys that the migration 0240_tracker_foreign_keys adds, each ON DELETE CASCADE: deleting a Team removes
//   its Projects, their WorkItems and their Comments (chapter 15 shows it; chapter 12 reads across the same chain
//   with nested Db.Exists).
//
// Why Managed = false
//   The generator writes a baseline migration (0000_baseline_<assembly>) from the entities as they are TODAY. A
//   database created from scratch would therefore get the final shape in one step, and a hand-written change such
//   as RenameColumn("projects", "legacy_code", "code") would name a column that never existed there. So for the
//   tracker the schema HISTORY is the explicit [Migration] classes in Migrations.cs (0200 to 0280), which run in
//   version order on every database, and the entities only describe the shape the history has reached.
//   The entities of Note.cs, Trash.cs and Ledger.cs are left Managed (the default): their tables come from the
//   generated baseline, which is the normal, simple path for a table that is only ever created, never altered.
//
// Reading the properties against the migrations
//   The properties are the shape AFTER all migrations: Team.Region and Project.Settings were added by 0220, and
//   Project has no property for the column that was called legacy_code, renamed to code (0230) and dropped (0260):
//   the entity never maps a column that no longer exists.

using Neemle.Orm.Lite;

namespace OrmLiteDemo;

// A team: the top of the chain. Table "teams".
// Region has a column default of 'eu' (added by migration 0220), which is why the first row, written when the
// table had only id and name, reads back with Region "eu" (chapter 8).
[Table("teams", Managed = false)]
public sealed class Team
{
    [Key] public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string Region { get; set; } = "";
}

// A project of a team. Table "projects". TeamId points at teams.id (foreign key fk_projects_team, cascade).
// The pair (team_id, title) is unique (index ux_projects_team_title, migration 0250): a team cannot have two
// projects with the same title; chapter 10 inserts a duplicate on purpose and the database refuses it.
[Table("projects", Managed = false)]
public sealed class Project
{
    [Key] public Guid Id { get; set; }

    public Guid TeamId { get; set; }

    public string Title { get; set; } = "";

    // A JSON document per project (added by 0220 with the default '{}'). Chapter 13 filters, orders and updates by
    // keys inside it. Jsonb.Parse throws if the text is not valid JSON.
    public Jsonb Settings { get; set; } = Jsonb.Parse("{}");
}

// A task of a project. Table "work_items". ProjectId points at projects.id (fk_work_items_project, cascade).
// Points is an estimate, Status is plain text ("open", "done" or "blocked" in the seed data; it is not an enum),
// and CreatedAt is a UTC timestamp. Points, Status and CreatedAt have column defaults in the table (0, 'open',
// the current time), which only matter for rows written without them; the typed inserts of this demo give every value.
[Table("work_items", Managed = false)]
public sealed class WorkItem
{
    [Key] public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Title { get; set; } = "";

    public int Points { get; set; }

    public string Status { get; set; } = "open";

    public DateTime CreatedAt { get; set; }
}

// A comment on a work item: the bottom of the chain. Table "comments". WorkItemId points at work_items.id
// (fk_comments_work_item, cascade).
[Table("comments", Managed = false)]
public sealed class Comment
{
    [Key] public Guid Id { get; set; }

    public Guid WorkItemId { get; set; }

    public string Body { get; set; } = "";
}
