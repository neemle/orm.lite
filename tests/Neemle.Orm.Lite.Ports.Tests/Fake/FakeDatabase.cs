using System.Data;
using System.Globalization;

namespace Neemle.Orm.Lite.Ports.Tests.Fake;

// The whole server of the fake provider: it records what reaches it, remembers which migration versions the
// history table holds, and answers a read of a registered table with the rows it was given. It parses no SQL.
public sealed class FakeDatabase
{
    private readonly object _gate = new();
    private readonly List<string> _events = new();
    private readonly List<FakeStatement> _statements = new();
    private readonly List<string> _applied = new();
    private readonly Dictionary<string, DataTable> _tables = new(StringComparer.Ordinal);

    // What a write that is not a history write reports as the number of affected rows.
    public int Affected { get; set; } = 1;

    public IReadOnlyList<string> Events
    {
        get
        {
            lock (_gate) return _events.ToList();
        }
    }

    public IReadOnlyList<FakeStatement> Statements
    {
        get
        {
            lock (_gate) return _statements.ToList();
        }
    }

    public IReadOnlyList<string> Applied
    {
        get
        {
            lock (_gate) return _applied.ToList();
        }
    }

    public IReadOnlyList<string> Sql() => Statements.Select(s => s.Text).ToList();

    public void Serve(string table, DataTable rows)
    {
        lock (_gate) _tables[table] = rows;
    }

    internal void Note(string text)
    {
        lock (_gate) _events.Add(text);
    }

    internal int Execute(string text, IReadOnlyDictionary<string, object> values)
    {
        Record(text, values);
        lock (_gate)
        {
            if (!MentionsHistory(text)) return Affected;
            if (text.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)) _applied.Add(VersionOf(values));
            else if (text.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)) _applied.Remove(VersionOf(values));
            return 1;
        }
    }

    internal DataTable Ask(string text, IReadOnlyDictionary<string, object> values)
    {
        Record(text, values);
        lock (_gate)
        {
            if (MentionsHistory(text)) return Versions();
            foreach (var (name, rows) in _tables)
                if (text.Contains(Quoted(name), StringComparison.Ordinal)) return rows.Copy();
            return new DataTable();
        }
    }

    private void Record(string text, IReadOnlyDictionary<string, object> values)
    {
        lock (_gate)
        {
            _statements.Add(new FakeStatement(text, values));
            _events.Add("sql:" + text);
        }
    }

    private static bool MentionsHistory(string text) =>
        text.Contains(Quoted(MigrationRunner.HistoryTable), StringComparison.Ordinal);

    private static string Quoted(string name) => "\"" + name + "\"";

    private static string VersionOf(IReadOnlyDictionary<string, object> values) =>
        values.TryGetValue("v", out var version) ? Convert.ToString(version, CultureInfo.InvariantCulture) : "";

    private DataTable Versions()
    {
        var table = new DataTable();
        table.Columns.Add(MigrationRunner.VersionColumn, typeof(string));
        foreach (var version in _applied) table.Rows.Add(version);
        return table;
    }
}
