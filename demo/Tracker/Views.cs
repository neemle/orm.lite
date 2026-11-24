// What this file shows
//   Database VIEWS, defined with the same typed query layer that reads tables, so no SQL text is written. A view
//   is a stored, read-only query; here two of them sit on the tracker tables:
//       open_work         the work items that are not done (open or blocked), joined with their project and team,
//                         one row per item with the item's title, its project's title, its team's name and its points
//       open_work_totals  one row: how many items open_work holds and the sum of their points (a view over a view)
//   The migration 0280 at the bottom creates them; chapter 14 (Chapters.cs) reads them, replaces open_work with a
//   narrower definition, checks the stored definition hash, and shows that a write through a view is refused.
//
// Library features exercised here
//   [View(name)]: an entity class that stands for a view instead of a table. The generator writes OpenWorkOrm with
//       Table, Descriptor, Map, Column and Query only: there is no Get and no Values, and InsertAsync, UpdateAsync and
//       DeleteAsync exist only as members marked [Obsolete(..., true)], so calling one does not compile (and a write
//       built on Query<T> is refused at run time), because the view is read-only. The view entity is never created
//       as a table by the generated baseline.
//   MigrationBuilder.From<T>().Where(...).AsView<TView>(...): a view definition built from a typed query. From<T>() is
//       a query bound to the provider that is running the migration; AsView maps the view's columns from the columns
//       of the base table and of the tables joined to it.
//   MigrationBuilder.CreateViews(...): creates views in dependency order (a view after every view it reads), whatever
//       order they are listed in, and the migration's Down is derived (DropView, in reverse).
//   Capability ObjectMarker (Native on PostgreSQL, Emulated on SQLite): the library stores a hash of each view's
//       definition in the database, which the catalog reads back; a definition that changed in code but was not
//       re-applied then shows up as drift. Chapter 14 prints the comparison.
//   A view stores its query, so each value in the Where (here "done") is written into the definition as a literal.

using System.Linq.Expressions;
using Neemle.Orm.Lite;

namespace OrmLiteDemo;

// The view entity for open_work. Plain properties: the names are the view's columns (id, title, project, team,
// points) and every one of them must be filled by the definition below, or building the definition throws.
[View("open_work")]
public sealed class OpenWork
{
    public Guid Id { get; set; }

    public string Title { get; set; } = "";

    public string Project { get; set; } = "";

    public string Team { get; set; } = "";

    public int Points { get; set; }
}

// The view entity for open_work_totals: a single row of aggregates (a count and a sum), both held as long.
[View("open_work_totals")]
public sealed class OpenWorkTotals
{
    public long Items { get; set; }

    public long Points { get; set; }
}

internal static class TrackerViews
{
    // The definition of open_work. `open` is a parameter so the same definition can be built twice: migration 0280
    // passes "status is not done", and chapter 14 passes "status is open" to REPLACE the view with a narrower one.
    public static ViewDefinition OpenWork(MigrationBuilder b, Expression<Func<WorkItem, bool>> open) =>
        // Start from work_items (the base table) and keep only the rows the caller's predicate selects.
        b.From<WorkItem>()
            .Where(open)
            .AsView<OpenWork>(c => c
                // Inner joins. Join<Project, WorkItem>((w, p) => ...) joins projects (the first type argument) onto
                // what is already in the query (work_items, the second); in the lambda w is the existing table and p
                // the joined one. The second join hangs teams onto projects the same way.
                .Join<Project, WorkItem>((w, p) => p.Id == w.ProjectId)
                .Join<Team, Project>((p, t) => t.Id == p.TeamId)
                // One line per view column: the view property on the left, the source column on the right. The
                // type argument says which table a source column belongs to (the base table when it is omitted).
                .Column(v => v.Id, w => w.Id)
                .Column(v => v.Title, w => w.Title)
                .Column<Project>(v => v.Project, p => p.Title)
                .Column<Team>(v => v.Team, t => t.Name)
                .Column(v => v.Points, w => w.Points));

    // The definition of open_work_totals: it reads the VIEW open_work (b.From<OpenWork>()), not a table, and has no
    // joins. Count fills Items with count(*) and Sum fills Points with the sum of the points of the rows it reads. With no
    // grouping, an aggregate view yields exactly one row, which chapter 14 reads with Only(...).
    public static ViewDefinition Totals(MigrationBuilder b) =>
        b.From<OpenWork>().AsView<OpenWorkTotals>(c => c.Count(v => v.Items).Sum(v => v.Points, w => w.Points));
}

// 0280: creates both views. They are listed with the dependent one first on purpose: CreateViews orders them itself, so
// open_work is created before open_work_totals, which reads it. The inverse (drop both, the dependent one first) is
// derived, so there is no Down.
[Migration("0280_tracker_open_work_views", Reversibility.Reversible)]
public sealed class TrackerOpenWorkViews : Migration
{
    public override void Up(MigrationBuilder b) =>
        b.CreateViews(TrackerViews.Totals(b), TrackerViews.OpenWork(b, w => w.Status != "done"));
}
