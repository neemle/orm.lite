namespace Neemle.Orm.Lite;

    public sealed class PgDdlDialect : IDdlDialect
    {
        public string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

        public string Literal(object? value) => value == null ? "NULL"
            : value is string s ? "'" + s.Replace("'", "''") + "'"
            : value is bool b ? (b ? "true" : "false")
            : value.ToString()!;
    }
