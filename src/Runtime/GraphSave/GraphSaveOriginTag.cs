namespace Neemle.OrmLite;

internal sealed class GraphSaveOriginTag(bool partial)
{
    private int _partial = partial ? 1 : 0;
    internal bool Partial => Volatile.Read(ref _partial) != 0;
    internal void MarkPartial() => Interlocked.Exchange(ref _partial, 1);
}
