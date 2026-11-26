namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaDefinition
{
    internal static bool IsVirtual(string? sql) => sql is not null && SqliteSqlText.Tokenize(sql)
        .TakeWhile(t => !t.IsSymbol('(')).Any(t => t.IsWord("VIRTUAL"));

    internal static ParsedTable? Parse(string? sql)
    {
        if (sql is null) return null;
        try { return SqliteSqlText.ParseTable(sql); }
        catch (InvalidOperationException) { return null; }
    }

    internal static SchemaFact<string?> Collation(TableDefinition? definition)
    {
        if (definition is null) return SchemaFact<string?>.Unknown("column-definition");
        var tokens = definition.Tokens;
        var hits = Enumerable.Range(0, tokens.Count).Where(i => tokens[i].IsWord("COLLATE")).ToArray();
        if (hits.Length == 0) return SchemaFact<string?>.Known(null);
        if (hits.Length != 1 || hits[0] + 1 >= tokens.Count)
            return SchemaFact<string?>.Unknown("collation-grammar");
        var next = tokens[hits[0] + 1];
        return next.Kind is SqlTokenKind.Word or SqlTokenKind.Identifier
            ? SchemaFact<string?>.Known(next.Value) : SchemaFact<string?>.Unknown("collation-grammar");
    }

    internal static string Stamp(TableDefinition definition) =>
        SqliteSchemaRead.Hash(string.Join("|", definition.Tokens.Select(t => t.Kind + ":" + t.Value)));

    internal static SchemaFact<bool> Identity(TableDefinition? definition) => definition is null
        ? SchemaFact<bool>.Unknown("column-definition")
        : SchemaFact<bool>.Known(definition.Tokens.Any(t => t.IsWord("AUTOINCREMENT")));
}
