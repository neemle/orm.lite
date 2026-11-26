using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming;

public sealed class PostgresStreamingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [SkippableFact(DisplayName = "[UC-010] real Postgres pull stream preserves rows and resource truth")]
    public async Task Pull_parity_and_lifetime()
    {
        fixture.RequireAvailable();
        await StreamingProviderChecks.PullAsync(fixture);
    }

    [SkippableFact(DisplayName = "[UC-010] real Postgres projection stream preserves rows and resource truth")]
    public async Task Projection_parity_and_lifetime()
    {
        fixture.RequireAvailable();
        await StreamingProviderChecks.ProjectionAsync(fixture);
    }

    [SkippableFact(DisplayName = "[UC-010] real Postgres cancellation stream preserves rows and resource truth")]
    public async Task Cancellation_parity_and_lifetime()
    {
        fixture.RequireAvailable();
        await StreamingProviderChecks.CancellationAsync(fixture);
    }

    [SkippableFact(DisplayName = "[UC-010] real Postgres borrowed stream preserves rows and resource truth")]
    public async Task Borrowed_parity_and_lifetime()
    {
        fixture.RequireAvailable();
        await StreamingProviderChecks.BorrowedAsync(fixture);
    }
}
