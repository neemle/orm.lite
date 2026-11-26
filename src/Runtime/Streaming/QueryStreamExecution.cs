namespace Neemle.OrmLite;

internal static class QueryStreamExecution
{
    internal static QueryStreamEnumerator<T> Create<T>(StreamReadSnapshot<T> snapshot,
        CancellationToken terminalToken, CancellationToken enumerationToken) =>
        new(snapshot, terminalToken, enumerationToken);
}
