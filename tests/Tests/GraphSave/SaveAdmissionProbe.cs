using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SaveAdmissionProbe
{
    internal static async Task Run(Func<OrmContext, Task<GraphSaveResult>> save, GraphSaveError? refusal)
    {
        await using var source = new SaveProbeSource();
        await using var context = new OrmContext(source, PostgresProvider.Instance);
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => save(context));
        Assert.Equal(GraphSaveOutcome.NotStarted, error.Outcome);
        Assert.Equal(refusal ?? GraphSaveError.ProviderFailure, error.Error);
        Assert.Equal(refusal is null ? 1 : 0, source.Opens);
        if (refusal is null) Assert.Same(source.Stop, error.InnerException);
    }
}
