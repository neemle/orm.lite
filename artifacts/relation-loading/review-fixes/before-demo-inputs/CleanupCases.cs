using System.Data.Common;
using System.Runtime.CompilerServices;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class CleanupCases
{
    private static Exception? _mappingError;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Author ThrowMapper(DbDataReader _) => throw _mappingError!;
    internal static async Task VerifyAsync(Fixture f)
    {
        await MapperPrimaryAsync(f);
        await OpenPrimaryAsync(f);
        await SoleCleanupAsync(f);
    }
    private static async Task MapperPrimaryAsync(Fixture f)
    {
        var plan = new CleanupFaultPlan();
        await using var source = new CleanupFaultSource(f.Context.DataSource, plan);
        await using var context = new OrmContext(source, f.Context.Provider);
        _mappingError = plan.Primary;
        Exception error;
        try { error = await Check.ThrowsAsync<MapSentinelException>(() => new Query<Author>(context, ThrowMapper).ToListAsync()); }
        finally { _mappingError = null; }
        Check.That(ReferenceEquals(error, plan.Primary) && error.StackTrace!.Contains(nameof(ThrowMapper)),
            "mapper primary object/type/original frame survives dual cleanup failures");
        Check.That(plan.Closes == 1 && plan.Disposes == 1, "mapper failure still attempts close AND dispose");
        Check.That(error.Data.Count == 0, "secondary cleanup errors are not exposed through public exception data");
    }
    private static async Task OpenPrimaryAsync(Fixture f)
    {
        var plan = new CleanupFaultPlan();
        await using var source = new CleanupFaultSource(f.Context.DataSource, plan);
        await using var context = new OrmContext(source, f.Context.Provider);
        await context.DisposeAsync();
        var error = await Check.ThrowsAsync<RelationScopeException>(() => LoadingScope.OpenAsync(context, Fixture.Options));
        Check.That(error.Error == RelationScopeError.Closed, "registration primary survives owned open cleanup failures");
        Check.That(plan.Closes == 1 && plan.Disposes == 1, "failed open still attempts close AND dispose");
    }
    private static async Task SoleCleanupAsync(Fixture f)
    {
        var plan = new CleanupFaultPlan { FailClose = false };
        await using var source = new CleanupFaultSource(f.Context.DataSource, plan);
        await using var context = new OrmContext(source, f.Context.Provider);
        var error = await Check.ThrowsAsync<CleanupSentinelException>(() =>
            AuthorOrm.Query(context).ToListAsync(new(EagerBehavior.Suppress)));
        Check.That(ReferenceEquals(error, plan.DisposeError), "sole cleanup failure is propagated");
        Check.That(plan.Closes == 1 && plan.Disposes == 1, "sole cleanup still attempts both operations");
    }
}
