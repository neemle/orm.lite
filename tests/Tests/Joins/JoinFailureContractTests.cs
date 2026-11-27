using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

public sealed class JoinFailureContractTests
{
    [Theory(DisplayName = "[UC-008][SEC-114-02] safe read failures preserve privileged originals without formatting secrets")]
    [InlineData(JoinError.ProviderFailure)] [InlineData(JoinError.MappingFailure)]
    [InlineData(JoinError.WrongSlot)] [InlineData(JoinError.MissingValue)]
    public void Safe_error_formatting_does_not_automatically_publish_original_failures(JoinError error)
    {
        var cause = new InvalidOperationException("SQL=SELECT private_row WHERE key=key-secret; Password=credential-secret",
            new Exception("nested row-secret"));
        cause.Data["parameter-secret"] = "payload-secret";
        var failure = new JoinReadException(error, cause);
        Assert.Equal(error, failure.Error); Assert.Same(cause, failure.InnerException);
        Assert.Contains(error.ToString(), failure.Message, StringComparison.Ordinal);
        Private(cause, failure);
    }

    [Fact(DisplayName = "[UC-008][AC-114-08][SEC-114-02] canceled failure envelopes retain the exact requested token and original safely")]
    public async Task Cancellation_envelope_preserves_requested_token_and_original_identity()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var cause = new OperationCanceledException("SQL=SELECT private_row; Password=credential-secret", source.Token);
        cause.Data["parameter-secret"] = "payload-secret";
        var failure = new JoinReadCanceledException(source.Token, cause);
        var observed = await Assert.ThrowsAsync<JoinReadCanceledException>(() => Task.FromException(failure));
        Assert.Same(failure, observed); Assert.Same(cause, observed.InnerException);
        Assert.Equal(source.Token, observed.CancellationToken);
        Assert.True(observed.CancellationToken.IsCancellationRequested);
        Private(cause, observed);
    }

    private static void Private(Exception cause, Exception safe)
    {
        Assert.Same(cause, safe.InnerException); Assert.Empty(safe.Data);
        foreach (var secret in new[] { "SELECT", "private_row", "key-secret", "credential-secret", "row-secret", "parameter-secret", "payload-secret" })
        {
            Assert.DoesNotContain(secret, safe.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, safe.ToString(), StringComparison.Ordinal);
        }
        Assert.NotEmpty(cause.Message); Assert.NotEmpty(cause.Data);
    }
}
