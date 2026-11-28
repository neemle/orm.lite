namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// One dispatch of a scripted command: what ran, with which timeout and token, the how-many-th time.
internal sealed record Dispatch(string Text, int CommandTimeout, CancellationToken Token, int Attempt);
