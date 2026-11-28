using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.3: the execution rule is a boundary. In src/Runtime/Maintenance only MaintenanceExecution*.cs may
// await, be async, dispatch a command, open a connection, enter the writer gate or make a CancellationTokenSource, so
// an operation added later (Train B) cannot grow a pipeline of its own. The scan reads syntax, not text: a comment, a
// string or a declaration (the port's TryEnterWriteAsync) is no use.
public sealed class ExecutionBoundaryTests
{
    private static readonly HashSet<string> Effects = new(StringComparer.Ordinal)
    {
        "ExecuteReaderAsync", "ExecuteNonQueryAsync", "ExecuteScalarAsync", "OpenConnectionAsync", "TryEnterWriteAsync",
        "CancellationTokenSource",
    };

    [Fact(DisplayName = "ORML-137 D14.3 in src/Runtime/Maintenance only MaintenanceExecution*.cs awaits, dispatches, "
        + "opens, enters the gate or links tokens")]
    public void Only_the_executor_runs_effects()
    {
        var folder = Path.Combine(RuntimeSources.LibraryRoot(), "src", "Runtime", "Maintenance");
        var files = Directory.GetFiles(folder, "*.cs")
            .ToDictionary(f => Path.GetFileName(f), f => Uses(File.ReadAllText(f)));

        var outside = files.Where(f => !f.Key.StartsWith("MaintenanceExecution", StringComparison.Ordinal))
            .SelectMany(f => f.Value.Select(use => f.Key + ": " + use));
        Assert.Empty(outside);
        // The scan sees what it looks for: the executor itself does all of it.
        var inside = files.Where(f => f.Key.StartsWith("MaintenanceExecution", StringComparison.Ordinal))
            .SelectMany(f => f.Value)
            .ToHashSet();
        Assert.Superset(new HashSet<string>(["await", "async", .. Effects]), inside);
    }

    [Theory(DisplayName = "ORML-137 D14.3 the boundary scan finds each effect in code and none in a comment, a string "
        + "or a declaration")]
    [InlineData("async Task F() { await G(); }", "async,await")]
    [InlineData("Task F(DbCommand c) => c.ExecuteReaderAsync();", "ExecuteReaderAsync")]
    [InlineData("Task F(DbCommand c) => c.ExecuteNonQueryAsync(default);", "ExecuteNonQueryAsync")]
    [InlineData("Task F(DbCommand c) => c.ExecuteScalarAsync();", "ExecuteScalarAsync")]
    [InlineData("object F(DbDataSource s) => s.OpenConnectionAsync();", "OpenConnectionAsync")]
    [InlineData("object F(IMaintenanceProvider p) => p.TryEnterWriteAsync(null, default, default);",
        "TryEnterWriteAsync")]
    [InlineData("object F() => new CancellationTokenSource();", "CancellationTokenSource")]
    [InlineData("object F(Func<Task> f) => f;  // await an async ExecuteReaderAsync", "")]
    [InlineData("string F() => \"await async OpenConnectionAsync\";", "")]
    [InlineData("ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource s, TimeSpan t, CancellationToken c);",
        "")]
    public void The_scan_reads_syntax(string member, string expected)
    {
        var uses = Uses("class C { " + member + " }");

        Assert.Equal(expected.Length == 0 ? [] : expected.Split(','), uses);
    }

    // What a source file does that only the executor may: the await and async keywords, and references (never
    // declarations) to the effect members and to CancellationTokenSource.
    private static List<string> Uses(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var keywords = root.DescendantTokens()
            .Where(t => t.IsKind(SyntaxKind.AwaitKeyword) || t.IsKind(SyntaxKind.AsyncKeyword))
            .Select(t => t.Text);
        var references = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(n => n.Identifier.Text)
            .Where(Effects.Contains);
        return [.. keywords.Concat(references).Distinct().OrderBy(u => u == "async" ? 0 : u == "await" ? 1 : 2)];
    }
}
