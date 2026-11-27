namespace Neemle.OrmLite;

internal sealed class OrphanStatementBudget(int maximum)
{
    private int _used;
    private int _guard;

    internal void ObserveGuard(int cumulative)
    {
        if (cumulative < _guard) throw OrphanRefusal.At(OrphanError.UnsupportedGuard);
        Reserve(cumulative - _guard);
        _guard = cumulative;
    }

    internal void Require(int count)
    {
        if (count < 0 || (long)_used + count > maximum) throw OrphanRefusal.At(OrphanError.LimitExceeded);
    }

    internal void Reserve(int count)
    {
        Require(count);
        _used += count;
    }
}
