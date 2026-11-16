using System.Reflection;
using System.Runtime.CompilerServices;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class OrmLiteRuntimeVisibilityRosterTests
{
    private static readonly string[] Roster =
    [
        "Neemle.Orm.Lite.Tests",
    ];

    [Fact(DisplayName = "the runtime assembly grants InternalsVisibleTo to exactly the Neemle.Orm.Lite.Tests assembly")]
    public void The_runtime_exposes_its_internals_to_exactly_the_library_test_assembly()
    {
        var granted = typeof(Query<>).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .Order()
            .ToArray();

        Assert.Equal(Roster, granted);
    }
}
