using System.Runtime.CompilerServices;
using Neemle.Orm.Lite;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;

internal static class SoftRootRender
{
    static SoftRootRender() => RuntimeHelpers.RunClassConstructor(typeof(SoftDeleteDb).TypeHandle);

    public static Query<SoftRoot> Roots() => new(
        new OrmContext(new NeverUsedDataSource(), new PgDialect()),
        SoftRoot.Map);
}
