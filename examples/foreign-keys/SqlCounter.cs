using Neemle.OrmLite;

namespace ForeignKeysDemo;

internal sealed class SqlCounter : IOrmDiagnosticsSink
{
    internal int Count { get; private set; }
    public void OnStatement(StatementEvent statement) => Count++;
    public void OnTransaction(TransactionEvent transaction) { }
}
