using System.Collections.Generic;
using System.Threading;

namespace Neemle.Orm.Lite.Tests;

internal sealed class StatementRecorder
{
    private readonly object _gate = new();
    private readonly List<Entry> _entries = new();
    private int _connections;

    public sealed record Entry(string Sql, IReadOnlyList<object?> Values, int ConnectionId, bool InTransaction);

    public IReadOnlyList<Entry> Statements
    {
        get
        {
            lock (_gate) return _entries.ToArray();
        }
    }

    public int ConnectionsCreated => Volatile.Read(ref _connections);

    public int NextConnectionId() => Interlocked.Increment(ref _connections);

    public void Add(Entry entry)
    {
        lock (_gate) _entries.Add(entry);
    }
}
