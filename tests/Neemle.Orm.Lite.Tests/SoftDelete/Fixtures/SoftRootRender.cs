using System.Runtime.CompilerServices;
using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;

internal static class SoftRootRender
{
    static SoftRootRender() => RuntimeHelpers.RunClassConstructor(typeof(SoftDeleteDb).TypeHandle);

    public static Query<SoftRoot> Roots() => new(
        TestContext.Of(new NeverUsedDataSource(), PostgresProvider.Instance),
        SoftRoot.Map);
}
