using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanGuardFailureContractTests
{
    [Fact]
    public void Provider_failure_primitive_keeps_privileged_cause_out_of_routine_text()
    {
        var primary = new InvalidOperationException("private-provider-detail");
        var error = Assert.IsType<OrphanException>(OrphanGuardFailure.Safe(primary, default));
        Assert.Equal(OrphanError.ProviderFailure, error.Error);
        Assert.Equal(OrphanOutcome.NotStarted, error.Outcome);
        Assert.Same(primary, error.InnerException);
        Assert.DoesNotContain("private-provider-detail", error.Message);
        Assert.DoesNotContain("private-provider-detail", error.ToString());
    }

    [Fact]
    public void Requested_cancellation_primitive_keeps_caller_token_and_privileged_cause()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var primary = new OperationCanceledException("private-cancel-detail", cancellation.Token);
        var error = Assert.IsType<OrphanCanceledException>(OrphanGuardFailure.Safe(primary, cancellation.Token));
        Assert.Equal(OrphanOutcome.NotStarted, error.Outcome);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Same(primary, error.InnerException);
        Assert.DoesNotContain("private-cancel-detail", error.Message);
        Assert.DoesNotContain("private-cancel-detail", error.ToString());
    }
}
