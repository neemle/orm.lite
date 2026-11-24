using System.Runtime.CompilerServices;
using Neemle.OrmLite;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.SoftDelete.Fixtures;

internal static class SoftRootRender
{
    static SoftRootRender() => RuntimeHelpers.RunClassConstructor(typeof(SoftDeleteDb).TypeHandle);

    public static Query<SoftRoot> Roots() => new(
        TestContext.Of(new NeverUsedDataSource(), PostgresProvider.Instance),
        SoftRoot.Map);
}
