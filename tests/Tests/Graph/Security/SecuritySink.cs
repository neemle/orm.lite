namespace Neemle.OrmLite.Tests.Graph.Security;

internal sealed class SecuritySink : IOrmDiagnosticsSink
{
    internal List<StatementEvent> Statements { get; } = [];
    internal List<TransactionEvent> Transactions { get; } = [];
    public void OnStatement(StatementEvent statement) => Statements.Add(statement);
    public void OnTransaction(TransactionEvent transaction) => Transactions.Add(transaction);
}
