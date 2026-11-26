// What this file shows
//   Chapters 8 to 16 of the demo: what you can do with a small, realistic schema (Team, Project, WorkItem, Comment; see
//   Entities.cs) once it exists. The schema itself was built by the migrations in Migrations.cs and Views.cs; these
//   chapters read it back and use it. Each chapter is one private method that prints its lines and returns true when
//   every value it read back was the expected one. TrackerChapters.RunAsync runs them in the order below and Program.cs
//   turns "all true" into "demo ok".
//
// Library features exercised here
//   ISchemaCatalog (db.Provider.CreateCatalog): reads the real database schema back (columns, foreign keys, indexes,
//       views), so a chapter can prove what a migration did. The same questions are answered on both providers.
//   UnitOfWork, Query<T>.On(work): several statements in one transaction.
//   MigrationBuilder / IndexTerm / ProviderRegistry: the node-tree DDL, serialised per provider.
//   Query<T> aggregates, Db.Exists, Db.Json* markers, GroupBy / Having, WhereIn: database-side computation from lambdas.
//   [View] entities and ViewDefinition: views as typed queries (Views.cs).
//   Capability, Native / Emulated / None: where a chapter has a SQLite versus PostgreSQL difference it is stated in
//       that chapter's heading. Differences are about HOW the database does the work (Emulated means the provider does
//       it with the same meaning); the printed lines are the same on both providers.
//
// The order matters
//   The chapters share one database and some change it: 9 moves points between work items and moves them back, 13 rewrites
//   a project's JSON settings, 14 replaces a view, 15 inserts and deletes its own rows. So chapter 8 runs first (it
//   expects the first rows exactly as the migrations left them, for example settings {}), and the expected numbers in 11,
//   12 and 16 hold because 9 put the points back and rolled back its abandoned unit of work.
//   Because of this the demo expects a fresh database on every run.
//
// The data the migrations seeded (Migrations.cs), which the numbers below refer to
//   teams        platform (region eu), growth (region us)
//   projects     core (team platform), billing (team growth)
//   work items   public api (core, open, 8), job queue (core, done, 5), invoice export (billing, open, 3), refund flow
//                (billing, blocked, 13)
//   comments     "needs a design" (public api), "csv first" (invoice export), "waiting for legal" (refund flow)

using System.Data.Common;
using System.Text.Json;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Neemle.OrmLite.Nodes;

namespace OrmLiteDemo;

internal static class TrackerChapters
{
    // Runs the chapters in order. Each call is awaited one after another (they share the database), and every chapter
    // runs even if an earlier one returned false, so one run shows all the output.
    public static async Task<bool> RunAsync(OrmContext db)
    {
        var schema = await SchemaAsync(db);
        var transactions = await TransactionsAsync(db);
        var indexes = await IndexesAsync(db);
        var aggregates = await AggregatesAsync(db);
        var cascade = await CascadeSelectAsync(db);
        var json = await JsonAsync(db);
        var views = await ViewsAsync(db);
        var cleanup = await CascadeDeleteAsync(db);
        var report = await GroupedReportAsync(db);
        var graph = await GraphChapter.RunAsync(db);
        return schema && transactions && indexes && aggregates && cascade
            && json && views && cleanup && report && graph;
    }

    // Chapter 8: the schema the tracker migrations left, read back from the database.
    // Purpose: prove what the migrations 0200 to 0260 did, by asking the database instead of trusting the migration text.
    // Notice: teams has gained "region" and projects "settings"; projects has neither legacy_code nor code (renamed in 0230,
    // dropped in 0260); the first rows survived with their data, and "platform" has region 'eu' that nobody wrote, it came
    // from the column default (0220). The foreign keys cascade, the indexes are the ones 0250 created minus the two 0260
    // dropped (ix_work_items_status by name, ix_projects_code with its column).
    // Prints: the column names of the two tables that changed, and what the first team and project read back as.
    // SQLite vs PostgreSQL: the catalog reads PRAGMA data on SQLite and the system catalogs on PostgreSQL, and hides it;
    // the output is the same. (The migrations that got here differ underneath: on SQLite 0240 rebuilt three tables.)
    private static async Task<bool> SchemaAsync(OrmContext db)
    {
        // A plain connection from the context's data source, and the catalog of the active provider over it.
        await using var connection = await db.DataSource.OpenConnectionAsync();
        var catalog = db.Provider.CreateCatalog(connection);
        async Task<string[]> NamesAsync(string table) => (await catalog.ColumnsAsync(table)).Select(c => c.Name).ToArray();
        var teams = await NamesAsync("teams");
        var projects = await NamesAsync("projects");
        var keys = await catalog.ForeignKeysAsync("work_items");
        var itemIndexes = await catalog.IndexesAsync("work_items");
        var projectIndexes = await catalog.IndexesAsync("projects");
        // The same rows through the generated repositories: the rows 0210 wrote, now with the later columns filled in.
        var platform = await TeamOrm.GetAsync(db, Ids.Platform);
        var core = await ProjectOrm.GetAsync(db, Ids.Core);
        Console.WriteLine($"8. entity changes by migration: teams({string.Join(", ", teams)}) projects({string.Join(", ", projects)})");
        Console.WriteLine($"   the first row kept its data: team '{platform?.Name}' has region '{platform?.Region}' from the column default, project '{core?.Title}' was renamed-column safe");
        // Expected shape: new columns present, old ones gone, defaults applied, a cascading foreign key onto projects,
        // the composite index with its two terms, the dropped index gone, the unique index unique, the code index gone.
        return teams.Contains("region")
            && projects.Contains("settings") && !projects.Contains("legacy_code") && !projects.Contains("code")
            && platform is { Name: "platform", Region: "eu" }
            && core is { Title: "core" } && core.Settings.Json == "{}"
            && keys.Any(k => k.ReferencedTable == "projects" && k.OnDelete == ReferentialAction.Cascade)
            && itemIndexes.Any(i => i.Name == "ix_work_items_project_points" && i.Terms.Count == 2)
            && !itemIndexes.Any(i => i.Name == "ix_work_items_status")
            && projectIndexes.Any(i => i.Name == "ux_projects_team_title" && i.Unique)
            && !projectIndexes.Any(i => i.Name == "ix_projects_code");
    }

    // Chapter 9: units of work, commit and rollback.
    // Purpose: show that several statements can succeed or fail as one, and what "rolls back" means.
    // Notice, in three parts. (1) Moving 2 points from "refund flow" to "invoice export" is two updates; it commits.
    // (2) Moving 100 the other way is refused by a business check, BEFORE commit: the two updates already sent are undone
    // when the unit of work is disposed without CommitAsync, and the sums read afterwards (11 and 5) are those of
    // part 1. (3) A unit of work inserts a comment and changes a work item's status, then throws; the exception leaves
    // the block with no commit, so the comment was never stored and the status is still 'open'.
    // Prints: whether each move committed, and the comment and status after the failure.
    // SQLite vs PostgreSQL: the output is the same. On SQLite a unit of work takes the database's single write lock when
    // it begins (one writer at a time); on PostgreSQL it is an ordinary transaction.
    private static async Task<bool> TransactionsAsync(OrmContext db)
    {
        // Part 1 and 2: move points, and a move that has to be refused. Then put the 2 points back for later chapters.
        var moved = await MoveAsync(db, Ids.Refund, Ids.Invoice, 2);
        var tooMuch = await MoveAsync(db, Ids.Invoice, Ids.Refund, 100);
        // Read OUTSIDE any unit of work, so it sees only what was committed: 11 and 5 proves the refused move left no trace.
        var afterRefused = await PointsAsync(db);
        var back = await MoveAsync(db, Ids.Invoice, Ids.Refund, 2);

        // Part 3: a failure partway through a unit of work.
        var abandoned = Guid.NewGuid();
        try
        {
            await using var work = await UnitOfWork.BeginAsync(db);
            // The overloads that take work.Connection run inside this transaction.
            await CommentOrm.InsertAsync(db, work.Connection, new Comment { Id = abandoned, WorkItemId = Ids.Api, Body = "never stored" });
            var item = (await WorkItemOrm.GetAsync(db, work.Connection, Ids.Api))!;
            item.Status = "done";
            await WorkItemOrm.UpdateAsync(db, work.Connection, item);
            // The "second step failed": leaving the block here, without CommitAsync, rolls everything back.
            throw new InvalidOperationException("the second step failed");
        }
        catch (InvalidOperationException)
        {
            // Expected; the demo catches it so it can look at what the database holds.
        }

        var comment = await CommentOrm.GetAsync(db, abandoned);
        var api = await WorkItemOrm.GetAsync(db, Ids.Api);
        var points = await PointsAsync(db);
        Console.WriteLine($"9. unit of work: moved 2 points and committed ({moved}), refused moving 100 and rolled back ({!tooMuch}), moved them back ({back})");
        Console.WriteLine($"   after the failure partway through a unit of work: comment stored = {comment is not null}, status = '{api?.Status}'");
        // (refund, invoice) points: 11 and 5 after the first move and the refused one, 13 and 3 again at the end.
        return moved && !tooMuch && back
            && afterRefused == (11, 5) && points == (13, 3)
            && comment is null && api is { Status: "open" };
    }

    // One move of points between two work items as one unit of work. Returns false (and commits nothing) when the move
    // would take an item below zero or the total changed.
    private static async Task<bool> MoveAsync(OrmContext db, Guid fromId, Guid toId, int points)
    {
        await using var work = await UnitOfWork.BeginAsync(db);
        // On(work) makes the query run on the unit of work's connection, so it reads what this transaction has written.
        var source = (await WorkItemOrm.Query(db).On(work).Where(w => w.Id == fromId).FirstOrDefaultAsync())!;
        var target = (await WorkItemOrm.Query(db).On(work).Where(w => w.Id == toId).FirstOrDefaultAsync())!;
        var before = source.Points + target.Points;
        source.Points -= points;
        target.Points += points;
        await WorkItemOrm.UpdateAsync(db, work.Connection, source);
        await WorkItemOrm.UpdateAsync(db, work.Connection, target);
        // An invariant read back from the database inside the transaction: points were moved, not created or lost.
        var after = await WorkItemOrm.Query(db).On(work).Where(w => w.Id == fromId || w.Id == toId).SumAsync(w => w.Points);
        // Refuse: return without CommitAsync. Disposing the unit of work (end of the method) rolls the updates back.
        if (source.Points < 0 || after != before) return false;
        await work.CommitAsync();
        return true;
    }

    // The points of "refund flow" and "invoice export", each a database-side SUM, read outside any unit of work.
    private static async Task<(long Refund, long Invoice)> PointsAsync(OrmContext db) =>
        (await WorkItemOrm.Query(db).Where(w => w.Id == Ids.Refund).SumAsync(w => w.Points),
         await WorkItemOrm.Query(db).Where(w => w.Id == Ids.Invoice).SumAsync(w => w.Points));

    // Chapter 10: indexes.
    // Purpose: show how an index with a descending column is declared, how each provider writes it, how the database
    // proves it holds, and that a unique index refuses a duplicate.
    // Notice: the printed CREATE INDEX text is built WITHOUT a database: the same MigrationBuilder call is rendered by
    // the PostgreSQL provider and by the SQLite provider, and here the two texts happen to be identical. A statement the
    // providers write differently would show as two different lines; one that a provider cannot express at all (nulls
    // first/last in an index: Native on PostgreSQL, None on SQLite) is refused by name instead. "declared with
    // [Index] on Note" lists indexes that Note.cs declared with attributes, read back from the catalog with their DESC.
    // The last line is two facts: the duplicate project insert was refused by the database (unique index
    // ux_projects_team_title), and the work item with the highest points in "billing" is "refund flow".
    // SQLite vs PostgreSQL: see above; the catalog Definition text differs (the stored CREATE INDEX on SQLite,
    // pg_get_indexdef on PostgreSQL) but the chapter only checks that it contains DESC.
    private static async Task<bool> IndexesAsync(OrmContext db)
    {
        // Render the same DDL for both providers; ProviderRegistry.Require finds a provider by id (both packages are referenced).
        var rendered = new List<string>();
        foreach (var id in new[] { "postgres", "sqlite" })
        {
            var provider = ProviderRegistry.Require(id);
            rendered.Add(new MigrationBuilder(provider).CreateIndex("ix_demo_sorted", "work_items", IndexTerm.Asc("project_id"), IndexTerm.Desc("points")).Render(provider).Single());
        }

        // A query that asks for the order the index serves: per project, highest points first.
        var top = await WorkItemOrm.Query(db)
            .Where(w => w.ProjectId == Ids.Billing)
            .OrderBy(w => w.ProjectId).ThenByDescending(w => w.Points)
            .FirstOrDefaultAsync();
        // A second project named "billing" for the same team breaks the unique (team_id, title) index; the provider's
        // driver raises a DbException (the type both SQLite's and PostgreSQL's driver exceptions derive from).
        var refused = false;
        try
        {
            await ProjectOrm.InsertAsync(db, new Project { Id = Guid.NewGuid(), TeamId = Ids.Growth, Title = "billing" });
        }
        catch (DbException)
        {
            refused = true;
        }

        // The two descending indexes that Note.cs declared by attribute: read their stored definition from the catalog.
        await using var connection = await db.DataSource.OpenConnectionAsync();
        var stored = (await db.Provider.CreateCatalog(connection).IndexesAsync("notes"))
            .Where(i => i.Name is "ix_notes_stars_title" or "ix_notes_stars" && i.Definition.Contains("DESC", StringComparison.Ordinal)).ToList();

        Console.WriteLine("10. composite index with a descending column, as each provider serialises it:");
        foreach (var text in rendered) Console.WriteLine("   " + text);
        Console.WriteLine("   declared with [Index] on Note, stored with the direction: " + string.Join(", ", stored.Select(i => i.Name)));
        Console.WriteLine($"   the unique (team_id, title) index refused a duplicate project: {refused}; highest-point item first: '{top?.Title}'");
        return rendered.All(t => t.Contains("(\"project_id\", \"points\" DESC)", StringComparison.Ordinal))
            && stored.Count == 2 && refused && top?.Id == Ids.Refund;
    }

    // Chapter 11: several aggregates in ONE statement.
    // Purpose: show that counting and summing happen in the database, not over objects loaded into memory.
    // Notice: one SELECT computes six values over the two work items of "billing": count 2, sum of points 16 (3 + 13),
    // minimum 3, 2 distinct statuses, and counts filtered by status ("open" 1 and "blocked" 1, which the database
    // evaluates as COUNT ... FILTER; capability AggregateFilter, Native on both providers).
    // Prints: those six numbers in the order they were asked.
    // SQLite vs PostgreSQL: the numeric types the two return differ (a sum of integers is bigint on PostgreSQL and an
    // integer on SQLite), which is why every cell is converted to long before it is compared.
    private static async Task<bool> AggregatesAsync(OrmContext db)
    {
        var row = await WorkItemOrm.Query(db)
            .Where(w => w.ProjectId == Ids.Billing)
            .AggregateCount()
            .AggregateSum(w => w.Points)
            .AggregateMin(w => w.Points)
            .AggregateCountDistinct(w => w.Status)
            .AggregateCountWhere(w => w.Status == "open")
            .AggregateCountWhere(w => w.Status == "blocked")
            .AggregatesAsync();
        var v = row.Select(Convert.ToInt64).ToArray();
        Console.WriteLine($"11. one statement over billing: count {v[0]}, sum {v[1]}, min {v[2]}, distinct statuses {v[3]}, open {v[4]}, blocked {v[5]}");
        return v.SequenceEqual([2, 16, 3, 2, 1, 1]);
    }

    // Chapter 12: a selection across four tables in one statement.
    // Purpose: answer "the comments on work items of projects of teams in region 'us'" without loading the parents.
    // Notice: Db.Exists<T>(predicate) is a marker that only means something inside a query lambda; it is translated to a
    // correlated EXISTS subquery. Nested three levels deep it gives one statement with three EXISTS (the demo counts them in
    // the SQL text the query renders, RenderSelect). The lambda's c, w, p and t are comments, work items, projects and teams; each
    // inner predicate refers to the row of the level above it (w.Id == c.WorkItemId). Only the team "growth" is in
    // 'us', so the result is its project's comments.
    // Prints: the count of EXISTS in the statement and the two comment bodies, sorted.
    // SQLite vs PostgreSQL: Native on both; the same statement shape.
    private static async Task<bool> CascadeSelectAsync(OrmContext db)
    {
        var query = CommentOrm.Query(db).Where(c =>
            Db.Exists<WorkItem>(w => w.Id == c.WorkItemId
                && Db.Exists<Project>(p => p.Id == w.ProjectId
                    && Db.Exists<Team>(t => t.Id == p.TeamId && t.Region == "us"))));
        // The SQL the query will run, as text, so the demo can count its EXISTS.
        var sql = query.RenderSelect();
        var comments = await query.OrderBy(c => c.Body).ToListAsync();
        Console.WriteLine($"12. comments of work items of projects of teams in 'us', one statement with {sql.Split("EXISTS").Length - 1} nested EXISTS: {string.Join(", ", comments.Select(c => c.Body))}");
        return comments.Select(c => c.Body).SequenceEqual(["csv first", "waiting for legal"])
            && sql.Split("EXISTS").Length - 1 == 3;
    }

    // Chapter 13: JSON documents.
    // Purpose: use the Jsonb column Project.Settings: filter by what is inside it, order by it, and change it in place.
    // Notice: the Db.Json* calls are markers translated to the database's JSON operators; the keys are top-level member names,
    // never paths. JsonKeyEquals says how to compare (JsonValueOrder.Text or Number: "wip" is compared as a number).
    // SetJsonMerge merges a JSON object into the stored document and SetJsonMemberIncrement adds to a numeric member, each in
    // one UPDATE with no read-modify-write in C#; an UPDATE needs a Where (the library refuses an unfiltered one). Core's
    // settings went from {} to owner "ann", wip 5, then wip 6. Reading a member back is done with System.Text.Json in the
    // program; a query cannot read a typed property of the document.
    // Prints: the projects each filter found (only "billing", whose seeded document has owner bob, wip 2 and a labels array
    // containing finance), then core's owner and wip after the changes, and the ordering by owner (ann before bob).
    // SQLite vs PostgreSQL: capability JsonDocument, Native on PostgreSQL (jsonb) and Emulated on SQLite (a text column
    // with functions the library registers on its connections); built to give the same results. SQLite's own JSON
    // operators differ from jsonb in ways that matter (number ordering, a key starting with $, null merging), which
    // is why the library does not use them.
    private static async Task<bool> JsonAsync(OrmContext db)
    {
        // Helper: run a project query with the given filter and return the project titles, sorted.
        async Task<string[]> TitlesAsync(Func<Query<Project>, Query<Project>> where) =>
            (await where(ProjectOrm.Query(db)).OrderBy(p => p.Title).ToListAsync()).Select(p => p.Title).ToArray();

        // Four ways to filter on the document: a text member, a numeric member, key presence, array membership.
        var byOwner = await TitlesAsync(q => q.Where(p => Db.JsonKeyEquals(p.Settings, "owner", "bob", JsonValueOrder.Text)));
        var byWip = await TitlesAsync(q => q.Where(p => Db.JsonKeyEquals(p.Settings, "wip", "2", JsonValueOrder.Number)));
        var hasLabels = await TitlesAsync(q => q.Where(p => Db.JsonHasKey(p.Settings, "labels")));
        var finance = await TitlesAsync(q => q.Where(p => Db.JsonArrayContainsString(p.Settings, "labels", "finance")));

        // Change core's document in place: merge in two members, then add 1 to wip. (MovedFieldKey names the member to
        // increment; it accepts lowercase letters, digits and underscores.)
        await ProjectOrm.Query(db).Where(p => p.Id == Ids.Core).SetJsonMerge(p => p.Settings, Jsonb.Parse("{\"owner\":\"ann\",\"wip\":5}")).UpdateAsync();
        await ProjectOrm.Query(db).Where(p => p.Id == Ids.Core).SetJsonMemberIncrement(p => p.Settings, new MovedFieldKey("wip"), 1).UpdateAsync();
        var core = (await ProjectOrm.GetAsync(db, Ids.Core))!;
        using var document = JsonDocument.Parse(core.Settings.Json);
        var wip = document.RootElement.GetProperty("wip").GetInt32();
        var owner = document.RootElement.GetProperty("owner").GetString();

        // Order by a key inside the document: OrderByJsonKey(column, key, descending, nullsFirst); ascending here, so "ann"
        // (core) comes before "bob" (billing).
        var ordered = (await ProjectOrm.Query(db)
            .Where(p => p.Id == Ids.Core || p.Id == Ids.Billing)
            .OrderByJsonKey(p => p.Settings, new JsonKey("owner"), false, false)
            .ToListAsync()).Select(p => p.Title).ToArray();

        Console.WriteLine($"13. json: owner=bob {string.Join(",", byOwner)}; wip=2 {string.Join(",", byWip)}; has labels {string.Join(",", hasLabels)}; label finance {string.Join(",", finance)}");
        Console.WriteLine($"   merged and incremented in place: core owner '{owner}' wip {wip}; ordered by owner: {string.Join(", ", ordered)}");
        return byOwner.SequenceEqual(["billing"]) && byWip.SequenceEqual(["billing"])
            && hasLabels.SequenceEqual(["billing"]) && finance.SequenceEqual(["billing"])
            && owner == "ann" && wip == 6 && ordered.SequenceEqual(["core", "billing"]);
    }

    // Chapter 14: views.
    // Purpose: use the two views that migration 0280 created (Views.cs), then change one, and show what a view will not do.
    // Notice, in order. (a) open_work holds the 3 work items that are not done, with project and team names from the joins; open_work_totals,
    // a view over that view, says 3 items and 24 points (8 + 3 + 13). (b) The definition of open_work is REPLACED by a
    // narrower one (only status "open") using the same CreateView node the migration used, with Replace set, applied
    // through MigrationRunner.ApplyAsync (which runs operations directly, without recording a migration version): the view now
    // holds 2 items, and open_work_totals, which was not touched, now reports 2 items and 11 points, because it reads
    // open_work. (c) The library stores a hash of each view's definition (capability ObjectMarker); after the replacement
    // the stored hash equals the hash of the NEW definition, which is how a definition that drifted from the code can be
    // detected. (d) A delete built on the view is refused with an exception before any statement is sent: a view is read-only.
    // Prints: the first line lists the view's rows as "title (team/project, points)"; the second the totals before and after
    // the replacement; the third whether the stored marker matches and whether the delete was refused.
    // SQLite vs PostgreSQL: the replacement is native on PostgreSQL (CREATE OR REPLACE VIEW keeps views built on it) and
    // emulated on SQLite (the provider drops and recreates the view; open_work_totals keeps working because
    // SQLite reads a view's text when the view is read). The markers are Native (a comment on the view) on PostgreSQL and
    // Emulated (a row in a table the library keeps) on SQLite.
    // Because this chapter leaves the narrower definition in the database, the demo is meant for a fresh database.
    private static async Task<bool> ViewsAsync(OrmContext db)
    {
        // A MigrationBuilder bound to the provider, used only to build a definition (nothing is run by it).
        var builder = new MigrationBuilder(db.Provider);
        // The expected size of open_work, counted on the base table with the same predicate the view was created with.
        var expected = await WorkItemOrm.Query(db).Where(w => w.Status != "done").CountAsync();
        var all = await OpenWorkOrm.Query(db).OrderBy(w => w.Points).ToListAsync();
        var totals = Only(await OpenWorkTotalsOrm.Query(db).ToListAsync());

        // The narrower definition: the same view with a different Where. CreateView(..., true) is "create or replace".
        var onlyOpen = TrackerViews.OpenWork(builder, w => w.Status == "open");
        await MigrationRunner.ApplyAsync(db.DataSource, db.Provider, [new CreateView(onlyOpen.View, onlyOpen.Query, true)]);
        var narrowed = await OpenWorkOrm.Query(db).OrderBy(w => w.Points).ToListAsync();
        var narrowedTotals = Only(await OpenWorkTotalsOrm.Query(db).ToListAsync());
        // The catalog returns the stored view with its marker, the hash of the definition recorded when it was created.
        await using var connection = await db.DataSource.OpenConnectionAsync();
        var stored = await db.Provider.CreateCatalog(connection).ViewAsync("open_work");

        // A write through a view cannot be built; this one throws InvalidOperationException before any statement is run.
        var refused = false;
        try
        {
            await OpenWorkOrm.Query(db).Where(w => w.Points > 0).DeleteAsync();
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        Console.WriteLine($"14. views: open_work joins work_items, projects and teams and holds {all.Count} items: {string.Join(", ", all.Select(w => $"{w.Title} ({w.Team}/{w.Project}, {w.Points})"))}");
        Console.WriteLine($"   open_work_totals reads open_work: {totals.Items} items, {totals.Points} points; after the definition was replaced by an open-only one: {narrowed.Count} items, totals {narrowedTotals.Items}/{narrowedTotals.Points}");
        Console.WriteLine($"   the stored marker is the hash of the new definition: {stored?.Marker == onlyOpen.Marker(db.Provider)}; a delete through the view is refused: {refused}");
        // Expected: the view had as many rows as the base query, the totals agree with the rows, the narrowed view is smaller
        // but not empty, its totals follow it, the stored marker is the new one, and the delete was refused.
        return all.Count == expected && totals.Items == all.Count && totals.Points == all.Sum(w => w.Points)
            && narrowed.Count > 0 && narrowed.Count < all.Count && narrowedTotals.Items == narrowed.Count && narrowedTotals.Points == narrowed.Sum(w => w.Points)
            && stored?.Marker == onlyOpen.Marker(db.Provider) && refused;
    }

    // The one row of an aggregate view; Single() throws if there is not exactly one.
    private static T Only<T>(List<T> rows) => rows.Single();

    // Chapter 15: ON DELETE CASCADE through three foreign keys.
    // Purpose: show that deleting a team removes everything below it, and that the database, not the program, does it.
    // Notice: the program inserts a team, a project, a work item and a comment (parents first, because the foreign keys need
    // the parent to exist), commits, deletes ONLY the team, and finds the comment and the work item gone. There is no loop
    // and no delete of the children in the program: that is what the cascading keys of migration 0240 are for. (This is a
    // real delete; the soft delete of chapters 5 to 7 is a different thing, a marker on a row.)
    // Prints: whether the comment existed before and after, and how many work items of the project are left.
    // SQLite vs PostgreSQL: AlterConstraint is Native on PostgreSQL and Emulated on SQLite (0240 rebuilt the tables there), and
    // SQLite enforces foreign keys on the connections the library opens, so the cascade happens on both.
    private static async Task<bool> CascadeDeleteAsync(OrmContext db)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "temporary", Region = "eu" };
        var project = new Project { Id = Guid.NewGuid(), TeamId = team.Id, Title = "temporary" };
        var item = new WorkItem { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "temporary", CreatedAt = DateTime.UtcNow };
        var comment = new Comment { Id = Guid.NewGuid(), WorkItemId = item.Id, Body = "temporary" };
        // The four inserts are one unit of work, so either the whole chain exists or none of it does.
        await using (var work = await UnitOfWork.BeginAsync(db))
        {
            await TeamOrm.InsertAsync(db, work.Connection, team);
            await ProjectOrm.InsertAsync(db, work.Connection, project);
            await WorkItemOrm.InsertAsync(db, work.Connection, item);
            await CommentOrm.InsertAsync(db, work.Connection, comment);
            await work.CommitAsync();
        }

        var before = await CommentOrm.GetAsync(db, comment.Id);
        // Delete the top of the chain only.
        await TeamOrm.DeleteAsync(db, team.Id);
        var after = await CommentOrm.GetAsync(db, comment.Id);
        var items = await WorkItemOrm.Query(db).Where(w => w.ProjectId == project.Id).CountAsync();
        Console.WriteLine($"15. deleting the team cascades through three foreign keys: comment before {before is not null}, after {after is not null}, items left {items}");
        return before is not null && after is null && items == 0;
    }

    // Chapter 16: grouped reports.
    // Purpose: GROUP BY, HAVING and ordering by an aggregate, done by the database, with typed results.
    // How it reads: GroupBy(key) starts a grouping. Each aggregate call (Count, Sum, Max, Avg, CountWhere) registers one output
    // column and returns a typed handle; the handle is what Having, OrderBy and the result row take, so nothing is
    // addressed by position or by a string. Where/WhereIn calls made before GroupBy filter the rows first.
    // Notice: three reports. (1) Work items per status, keeping only statuses whose points add up to more than 5, largest
    // sum first: "done" (5 points) is cut by HAVING, "blocked" (13) comes before "open" (8 + 3 = 11). The average is a double
    // on both providers. Max is asked as int? because the maximum of a group with no value is NULL. (2) Per project, keeping
    // projects with at least two items, by total points: billing (16) before core (13); each has 1 open of 2 items.
    // (3) Per status, but only for the work items whose project belongs to a team in region 'us': the query layer has no
    // join, so that is two nested subqueries (WhereIn inside WhereIn) that filter before grouping; the result is "blocked 1"
    // and "open 1" (the two billing items).
    // Prints: the three reports.
    // SQLite vs PostgreSQL: GROUP BY, HAVING and the aggregates are native on both. What differs underneath is the type a
    // database gives an average or a sum, which the library normalises (an average is a double on both).
    private static async Task<bool> GroupedReportAsync(OrmContext db)
    {
        // Report 1: per status.
        var byStatus = WorkItemOrm.Query(db).GroupBy(w => w.Status);
        var count = byStatus.Count();
        var points = byStatus.Sum<long>(w => w.Points);
        var biggest = byStatus.Max<int?>(w => w.Points);
        var average = byStatus.Avg(w => w.Points);
        // HAVING sum(points) > 5, ordered by the same sum, largest first. The calls only add terms; ToListAsync runs one statement.
        byStatus.Having(points, CompareOp.Greater, 5L).OrderBy(points, descending: true);
        var statuses = await byStatus.ToListAsync();

        // Report 2: per project.
        var perProject = WorkItemOrm.Query(db).GroupBy(w => w.ProjectId);
        var items = perProject.Count();
        var open = perProject.CountWhere(w => w.Status == "open");
        var total = perProject.Sum<long>(w => w.Points);
        perProject.Having(items, CompareOp.GreaterOrEqual, 2L).OrderBy(total, descending: true);
        var projects = await perProject.ToListAsync();

        // Report 3: per status, only for items of projects of teams in 'us'. WhereIn(column, subquery, selected column):
        // the item's ProjectId must be among the Ids of the projects the inner query selects, and that inner query in turn
        // selects projects whose TeamId is among the Ids of the teams in region 'us'.
        var inUs = WorkItemOrm.Query(db)
            .WhereIn<Project>(
                w => w.ProjectId,
                sub => sub.WhereIn<Team>(p => p.TeamId, teams => teams.Where(t => t.Region == "us"), t => t.Id),
                p => p.Id)
            .GroupBy(w => w.Status);
        var usCount = inUs.Count();
        var us = await inUs.OrderByKey().ToListAsync();

        Console.WriteLine("16. per status, one statement with GROUP BY and HAVING sum(points) > 5, largest sum first:");
        foreach (var row in statuses)
            Console.WriteLine($"   {row.Key}: {row.Get(count)} item(s), {row.Get(points)} points, largest {row.Get(biggest)}, average {row.Get(average):0.0}");
        Console.WriteLine($"   per project with at least two items: {string.Join("; ", projects.Select(p => $"{(p.Key == Ids.Billing ? "billing" : "core")} has {p.Get(open)} open of {p.Get(items)} items, {p.Get(total)} points"))}");
        Console.WriteLine($"   per status for the teams in region 'us' (two nested subqueries): {string.Join(", ", us.Select(r => $"{r.Key} {r.Get(usCount)}"))}");
        return statuses.Select(r => r.Key).SequenceEqual(["blocked", "open"])
            && statuses.Select(r => r.Get(count)).SequenceEqual([1L, 2L])
            && statuses.Select(r => r.Get(points)).SequenceEqual([13L, 11L])
            && statuses.Select(r => r.Get(biggest)).SequenceEqual([13, 8])
            && statuses.Select(r => r.Get(average)).SequenceEqual([13.0, 5.5])
            && projects.Select(p => p.Key).SequenceEqual([Ids.Billing, Ids.Core])
            && projects.Select(p => p.Get(total)).SequenceEqual([16L, 13L])
            && projects.All(p => p.Get(open) == 1 && p.Get(items) == 2)
            && us.Select(r => r.Key).SequenceEqual(["blocked", "open"])
            && us.All(r => r.Get(usCount) == 1);
    }
}
