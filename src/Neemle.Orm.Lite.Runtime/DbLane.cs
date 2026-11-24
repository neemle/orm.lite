#nullable enable
using System;
using System.Threading;

namespace Neemle.Orm.Lite;

public enum DbLaneKind { Background, Request, Ops }

public static class DbLane
{
    private static readonly AsyncLocal<DbLaneScope?> s_scope = new();

    public static DbLaneKind Current => CurrentScope?.Lane ?? DbLaneKind.Background;

    internal static DbLaneScope? CurrentScope
    {
        get
        {
            var scope = s_scope.Value;
            while (scope is { IsDisposed: true }) scope = scope.Previous;
            return scope;
        }
    }

    public static DbLaneScope Enter(DbLaneKind lane)
    {
        var scope = new DbLaneScope(lane, s_scope.Value);
        s_scope.Value = scope;
        return scope;
    }

    internal static void Leave(DbLaneScope scope)
    {
        if (ReferenceEquals(s_scope.Value, scope)) s_scope.Value = scope.Previous;
    }
}

public sealed class DbLaneScope : IDisposable
{
    private readonly DbLaneScope? _previous;
    private int _held;
    private int _peak;
    private int _disposed;

    internal DbLaneScope(DbLaneKind lane, DbLaneScope? previous)
    {
        Lane = lane;
        _previous = previous;
    }

    public DbLaneKind Lane { get; }

    public int HeldLeases => Volatile.Read(ref _held);

    public int PeakLeases => Volatile.Read(ref _peak);

    internal DbLaneScope? Previous => _previous;

    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal void Acquire()
    {
        var held = Interlocked.Increment(ref _held);
        var peak = Volatile.Read(ref _peak);
        while (held > peak)
        {
            var seen = Interlocked.CompareExchange(ref _peak, held, peak);
            if (seen == peak) return;
            peak = seen;
        }
    }

    internal void Release() => Interlocked.Decrement(ref _held);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            DbLane.Leave(this);
    }
}
