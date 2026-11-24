using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.DataLiterals;

// The text the PostgreSQL provider writes for one data value in a statement: the path that every public door of a
// literal (an InsertData cell, a DeleteData value) reaches.
internal static class PostgresLiteral
{
    internal static string Of(object? value) =>
        PostgresProvider.Instance.CreateSerializer().Fragment(DdlLiterals.Of(value));
}
