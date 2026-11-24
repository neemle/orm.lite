// What this file shows
//   How to see the SQL the library runs. SlowQueryReport is a diagnostics sink: an object the library calls for every
//   statement it executes. This one keeps them, and at the end of the demo prints how many there were and the slowest
//   few. It is chapter 18 of the output.
//
// Library features exercised here
//   IOrmDiagnosticsSink (OnStatement, OnTransaction): the interface a sink implements. Each StatementEvent carries the
//       SQL text the provider wrote, its Kind (Read, Write or Ddl), how long it took (Elapsed), whether it was Slow,
//       and more.
//   OrmLiteOptions.Diagnostics: the sink is code, so Program.cs assigns it (options.Diagnostics.Sink = report) BEFORE
//       OrmLite.OpenAsync. The settings that switch reporting on are data, and live in the "Diagnostics" block of
//       appsettings.json: StatementLevel "Debug" asks for every statement, SlowThresholdMilliseconds 5 marks a statement
//       that took 5 ms or more as Slow. (Asking for reports without assigning a sink is a start-up error.)
//   What reaches the sink: the statements of the repositories and the query layer, units of work, and the migration
//       runner's own DDL and bookkeeping; reads of the schema catalog that plan a migration do not.
//
// What to notice in the output
//   - The counts and the times differ from run to run and between providers (SQLite and PostgreSQL run different
//     statements; PostgreSQL's index reconcile step, for one, is a single DO block), so the demo does not compare
//     them with fixed numbers. It only requires that statements arrived, that each has text, and that parameter
//     values are absent (see Print).
//   - The statement text is what the database received, cut to 72 characters on one line for the printout.

using Neemle.Orm.Lite;

namespace OrmLiteDemo;

internal sealed class SlowQueryReport : IOrmDiagnosticsSink
{
    // Longest statement text printed; longer text is cut and ends in "...".
    private const int TextWidth = 72;

    // A statement may be reported from whichever thread ran it, so the list is guarded.
    private readonly object gate = new();

    private readonly List<StatementEvent> statements = [];

    // Called by the library after each statement.
    public void OnStatement(StatementEvent statement)
    {
        lock (gate) statements.Add(statement);
    }

    // Called for transaction begin, commit and rollback, if the settings ask for them (TransactionLevel). The demo's
    // settings do not, so this is never needed here.
    public void OnTransaction(TransactionEvent transaction)
    {
    }

    // Prints the summary line and the `top` slowest statements, and says whether the sink saw what it should have.
    public bool Print(int top)
    {
        List<StatementEvent> seen;
        lock (gate) seen = [.. statements];
        // Slow is set by the library for statements at or over SlowThresholdMilliseconds.
        var slow = seen.Count(s => s.Slow);
        Console.WriteLine($"18. statement report: {seen.Count} statement(s) reached the sink, {slow} over the slow threshold; the slowest:");
        foreach (var statement in seen.OrderByDescending(s => s.Elapsed).Take(top))
        {
            var text = statement.Text.ReplaceLineEndings(" ");
            if (text.Length > TextWidth) text = text[..TextWidth] + "...";
            Console.WriteLine($"   {statement.Elapsed.TotalMilliseconds,8:0.00} ms {statement.Kind,-5} {text}");
        }
        // Passes when at least one statement arrived, each carries its text, and none carries parameter values: values
        // are not reported unless the LogParameters setting is switched on, which the demo does not do.
        return seen.Count > 0 && seen.All(s => s.Text.Length > 0 && s.Parameters is null);
    }
}
