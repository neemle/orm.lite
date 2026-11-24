using Neemle.Orm.Lite;

namespace PackConsumer;

public static class LeaseProbe
{
    public static Task<AdvisorySessionLease?> Lease() => AdvisorySessionLocks.TryLeaseAsync(null!, null!, 1, default);
}
