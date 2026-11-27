namespace Neemle.OrmLite;

internal sealed record GraphSaveDiagnosticOutcome(TimeSpan Elapsed, int? Affected, bool Failed);
