using Neemle.OrmLite;
namespace ComposeProbe;
internal sealed class Counter : IOrmDiagnosticsSink
{
    internal int Count { get; private set; }
    public void OnStatement(StatementEvent statement) => Count++;
    public void OnTransaction(TransactionEvent transaction) { }
}
