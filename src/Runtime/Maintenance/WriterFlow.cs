namespace Neemle.OrmLite;

/// <summary>
/// Which writer gates the current async flow holds through an open <see cref="UnitOfWork"/>. The gate itself is an
/// ownerless semaphore, so this AsyncLocal chain is the only flow-scoped signal; a maintenance operation that would take
/// a gate the flow already holds refuses instead of waiting on itself. Modelled on <see cref="DbLane"/>.
/// </summary>
internal static class WriterFlow
{
    private static readonly AsyncLocal<WriterFlowMark?> s_mark = new();

    // Installed by the synchronous UnitOfWork.BeginAsync wrapper so the mark flows to the caller; a mark written inside an
    // async method would not.
    internal static WriterFlowMark Enter()
    {
        var mark = new WriterFlowMark(Live(s_mark.Value));
        s_mark.Value = mark;
        return mark;
    }

    internal static bool Holds(string key)
    {
        for (var mark = s_mark.Value; mark is not null; mark = mark.Previous)
            if (!mark.IsDisposed && string.Equals(mark.Key, key, StringComparison.Ordinal)) return true;
        return false;
    }

    // Closed marks on top of the chain are dropped when the next one is installed, so a flow that opens units of work
    // in a loop does not grow the chain.
    private static WriterFlowMark? Live(WriterFlowMark? mark)
    {
        while (mark is { IsDisposed: true }) mark = mark.Previous;
        return mark;
    }
}

internal sealed class WriterFlowMark(WriterFlowMark? previous)
{
    private string? _key;
    private int _disposed;

    internal WriterFlowMark? Previous { get; } = previous;

    internal string? Key
    {
        get => Volatile.Read(ref _key);
        set => Volatile.Write(ref _key, value);
    }

    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal void Close() => Interlocked.Exchange(ref _disposed, 1);
}
