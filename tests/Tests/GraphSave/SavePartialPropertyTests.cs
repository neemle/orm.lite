using System.Reflection;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SavePartialPropertyTests
{
    [Theory(DisplayName = "[UC-006][AC-112-07][SEC-112-02] partial getter refusal precedes getter and database I/O")]
    [InlineData("scalar")]
    [InlineData("navigation")]
    public async Task Partial_getters_are_refused_before_any_effect(string shape)
    {
        var assembly = await SaveSdkCompiler.Fixture();
        var witness = assembly.GetType("PartialSaveFixture.Witness", true)!;
        var probe = new SaveFaultProbe("partial");
        await using var source = new SaveFaultSource(probe);
        await using var context = new OrmContext(source, new SaveFaultProvider(probe));
        var error = await Record.ExceptionAsync(() => Save(witness, context, shape));
        var scalarReads = (int)witness.GetField("ScalarReads")!.GetValue(null)!;
        var navigationReads = (int)witness.GetField("NavigationReads")!.GetValue(null)!;
        Assert.True(error is GraphSaveException
            { Error: GraphSaveError.UnsupportedMapping, Outcome: GraphSaveOutcome.NotStarted }
            && scalarReads == 0 && navigationReads == 0 && probe.Opens == 0 && probe.Transaction is null
            && probe.Executions == 0 && probe.Writes == 0,
            $"{shape}: error={error?.GetType().Name ?? "none"}; scalar={scalarReads}; nav={navigationReads}; "
            + $"opens={probe.Opens}; tx={probe.Transaction is not null}; "
            + $"exec={probe.Executions}; writes={probe.Writes}");
    }

    [Fact(DisplayName = "[UC-006][AC-112-01] SDK-generated ordinary auto-property save remains admitted")]
    public async Task Ordinary_auto_properties_still_save()
    {
        var assembly = await SaveSdkCompiler.Fixture();
        var witness = assembly.GetType("PartialSaveFixture.Witness", true)!;
        var probe = new SaveFaultProbe("ordinary");
        await using var source = new SaveFaultSource(probe);
        await using var context = new OrmContext(source, new SaveFaultProvider(probe));
        var result = await Save(witness, context, "ordinary");
        Assert.Equal(GraphSaveOutcome.Committed, result.Outcome);
        Assert.Equal(1, probe.Opens);
        Assert.NotNull(probe.Transaction);
        Assert.Equal(1, probe.Executions);
        Assert.Equal(1, probe.CommittedWrites);
        Assert.Equal(0, witness.GetField("ScalarReads")!.GetValue(null));
        Assert.Equal(0, witness.GetField("NavigationReads")!.GetValue(null));
    }

    private static Task<GraphSaveResult> Save(Type witness, OrmContext context, string shape) =>
        (Task<GraphSaveResult>)witness.GetMethod("Save", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [context, shape])!;
}
