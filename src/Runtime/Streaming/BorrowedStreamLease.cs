using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Neemle.OrmLite;

internal sealed class BorrowedStreamLease : IAsyncDisposable
{
    private sealed class State { internal bool Busy; internal bool Unavailable; }
    private static readonly ConditionalWeakTable<DbConnection, State> States = new();
    private readonly State _state;
    private bool _released;

    private BorrowedStreamLease(State state) => _state = state;

    internal static BorrowedStreamLease TryAcquire(DbConnection connection)
    {
        var state = States.GetValue(connection, _ => new State());
        lock (state)
        {
            if (state.Unavailable) throw new QueryStreamException(QueryStreamError.ResourceUnavailable);
            if (state.Busy) throw new QueryStreamException(QueryStreamError.ConcurrentStream);
            state.Busy = true;
            return new BorrowedStreamLease(state);
        }
    }

    internal void Uncertain() { lock (_state) _state.Unavailable = true; }

    public ValueTask DisposeAsync()
    {
        lock (_state)
        {
            if (_released) return ValueTask.CompletedTask;
            _released = true;
            _state.Busy = false;
        }
        return ValueTask.CompletedTask;
    }
}
