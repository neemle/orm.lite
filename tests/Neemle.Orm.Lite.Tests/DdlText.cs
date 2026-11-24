using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests;

internal static class DdlText
{
    internal static IReadOnlyList<Statement> Statements(Node op) => DdlCompiler.Compile(op, PostgresProvider.Instance);

    internal static string Of(Node op) => Statements(op).Single().Text;

    internal static Statement Statement(Node op) => StatementCompiler.Compile(op, PostgresProvider.Instance, StatementKind.Read);
}
