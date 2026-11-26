using Neemle.OrmLite;

namespace PolicySmoke;

internal sealed class SpySink : IOrmDiagnosticsSink
{
    private readonly object _gate = new();
    private readonly List<StatementEvent> _statements = [];
    internal ReadPause? Pause { get; set; }
    internal StatementEvent[] Statements { get { lock (_gate) return _statements.ToArray(); } }
    internal int Count { get { lock (_gate) return _statements.Count; } }
    internal void Clear() { lock (_gate) _statements.Clear(); }

    public void OnStatement(StatementEvent statement)
    {
        lock (_gate) _statements.Add(statement);
        if (Pause is { } pause && statement.Text.Contains(pause.Contains, StringComparison.Ordinal))
            pause.Wait();
    }

    public void OnTransaction(TransactionEvent transaction) { }
}
