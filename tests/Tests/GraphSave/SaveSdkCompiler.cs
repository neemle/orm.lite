using System.Reflection;

namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SaveSdkCompiler
{
    private static readonly Lazy<Task<Assembly>> Compiled = new(CompileAsync);
    internal static Task<Assembly> Fixture() => Compiled.Value;

    private static Task<Assembly> CompileAsync() =>
        SdkFixtureCompiler.CompileFixture(SavePartialFixture.Source, "PartialSaveFixture");
}
