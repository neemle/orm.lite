namespace Neemle.OrmLite;

internal sealed class GraphSaveDiagnosticError() : Exception("Graph save statement failed.")
{
    public override string ToString() => Message;
}
