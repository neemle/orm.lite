using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests.DataLiterals;

// The text the PostgreSQL provider writes for one data value in a statement: the path that every public door of a
// literal (an InsertData cell, a DeleteData value) reaches.
internal static class PostgresLiteral
{
    internal static string Of(object? value) =>
        PostgresProvider.Instance.CreateSerializer().Fragment(DdlLiterals.Of(value));
}
