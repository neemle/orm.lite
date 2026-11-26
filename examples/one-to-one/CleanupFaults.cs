namespace OneToOneDemo;

internal sealed class CleanupFaults
{
    internal Exception ReaderError { get; } = new InvalidOperationException("controlled reader cleanup failure");
    internal Exception CommandError { get; } = new InvalidOperationException("controlled command cleanup failure");
    internal int ReaderAttempts { get; set; }
    internal int CommandAttempts { get; set; }
    internal int CallerDisposals { get; set; }
}
