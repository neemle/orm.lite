using System.Data;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrdinarySchemaGuardTests
{
    [Theory(DisplayName = "[UC-007][AC-113-02][AC-113-04] normal generated scalar defaults and nullable timestamp retain typed values through guard preflight")]
    [InlineData("postgres")] [InlineData("sqlite")]
    public async Task Generated_scalar_defaults_remain_ordinary_values(string provider)
    {
        await using var database = await OrdinaryOrphanDatabase.Open(provider, soft: false);
        await using var unit = await UnitOfWork.BeginAsync(database.Context);
        await database.AssertScalars(unit.Connection, unit.Transaction);
        await using var lease = await database.Guard.AcquireAsync(unit.Connection, unit.Transaction,
            OrdinaryOrphanProfiles.Request(false), default);
        Assert.Equal(new[] { GuardNativeModel.Key(11) }, lease.OriginalKeys.Cast<Guid>());
        await lease.VerifyEffectsAsync(OrphanMode.Nullify, default);
        await database.AssertScalars(unit.Connection, unit.Transaction);
        Assert.Equal(ConnectionState.Open, unit.Connection.State);
        await unit.Transaction.RollbackAsync();
    }

    [Theory(DisplayName = "[UC-007][AC-113-05] ordinary declared soft schema and generated live index admit soft-delete preflight")]
    [InlineData("postgres")] [InlineData("sqlite")]
    public async Task Generated_live_index_and_declared_soft_schema_are_compatible(string provider)
    {
        await using var database = await OrdinaryOrphanDatabase.Open(provider, soft: true);
        var index = provider == "postgres"
            ? "SELECT count(*) FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid "
                + "WHERE c.relname='ix_ordinary_soft_name' AND i.indisunique AND i.indpred IS NOT NULL"
            : "SELECT count(*) FROM pragma_index_list('ordinary_soft') "
                + "WHERE name='ix_ordinary_soft_name' AND \"unique\"=1 AND partial=1";
        Assert.Equal(1L, Convert.ToInt64(await database.Scalar(index)));
        await using var unit = await UnitOfWork.BeginAsync(database.Context);
        await database.AssertSoft(unit.Connection, unit.Transaction);
        await using var lease = await database.Guard.AcquireAsync(unit.Connection, unit.Transaction,
            OrdinaryOrphanProfiles.Request(true), default);
        Assert.Equal(new[] { GuardNativeModel.Key(11) }, lease.OriginalKeys.Cast<Guid>());
        await lease.VerifyEffectsAsync(OrphanMode.SoftDelete, default);
        await database.AssertSoft(unit.Connection, unit.Transaction);
        Assert.Equal(ConnectionState.Open, unit.Connection.State);
        await unit.Transaction.RollbackAsync();
    }
}
