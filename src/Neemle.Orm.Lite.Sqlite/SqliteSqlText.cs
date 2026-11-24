using System.Text;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

internal enum SqlTokenKind
{
    Word,
    Identifier,
    Literal,
    Symbol,
}

internal readonly record struct SqlToken(SqlTokenKind Kind, int Start, int End, string Value)
{
    internal bool IsWord(string word) => Kind == SqlTokenKind.Word && string.Equals(Value, word, StringComparison.OrdinalIgnoreCase);

    internal bool IsSymbol(char symbol) => Kind == SqlTokenKind.Symbol && Value.Length == 1 && Value[0] == symbol;

    internal bool Names(string name) =>
        Kind is SqlTokenKind.Word or SqlTokenKind.Identifier && string.Equals(Value, name, StringComparison.OrdinalIgnoreCase);
}

internal enum DefinitionKind
{
    Column,
    Constraint,
}

internal sealed record TableDefinition(string Text, DefinitionKind Kind, string? Name, IReadOnlyList<SqlToken> Tokens);

internal sealed record ParsedTable(IReadOnlyList<TableDefinition> Definitions, string Tail);

internal sealed record ForeignKeyText(
    string? Name, IReadOnlyList<string> Columns, string ReferencedTable, IReadOnlyList<string> ReferencedColumns,
    ReferentialAction OnDelete, ReferentialAction OnUpdate);

internal static class SqliteSqlText
{
    internal static IReadOnlyList<SqlToken> Tokenize(string sql)
    {
        var tokens = new List<SqlToken>();
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n') i++;
            }
            else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? sql.Length : end + 2;
            }
            else if (c is '"' or '`')
            {
                tokens.Add(Quoted(sql, ref i, c, c, SqlTokenKind.Identifier));
            }
            else if (c == '[')
            {
                tokens.Add(Quoted(sql, ref i, '[', ']', SqlTokenKind.Identifier));
            }
            else if (c == '\'')
            {
                tokens.Add(Quoted(sql, ref i, '\'', '\'', SqlTokenKind.Literal));
            }
            else if (char.IsLetterOrDigit(c) || c is '_' or '$')
            {
                var start = i;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] is '_' or '$')) i++;
                tokens.Add(new SqlToken(SqlTokenKind.Word, start, i, sql[start..i]));
            }
            else
            {
                tokens.Add(new SqlToken(SqlTokenKind.Symbol, i, i + 1, c.ToString()));
                i++;
            }
        }
        return tokens;
    }

    private static SqlToken Quoted(string sql, ref int i, char open, char close, SqlTokenKind kind)
    {
        var start = i++;
        var value = new StringBuilder();
        while (i < sql.Length)
        {
            if (sql[i] == close)
            {
                if (close != ']' && i + 1 < sql.Length && sql[i + 1] == close)
                {
                    value.Append(close);
                    i += 2;
                    continue;
                }
                i++;
                return new SqlToken(kind, start, i, value.ToString());
            }
            value.Append(sql[i++]);
        }
        return new SqlToken(kind, start, i, value.ToString());
    }

    internal static ParsedTable ParseTable(string sql)
    {
        var tokens = Tokenize(sql);
        var open = -1;
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].IsSymbol('(')) { open = i; break; }
        if (open < 0) throw new InvalidOperationException("the stored table definition has no column list.");

        var definitions = new List<TableDefinition>();
        var depth = 0;
        var begin = open + 1;
        var close = -1;
        for (var i = open; i < tokens.Count; i++)
        {
            if (tokens[i].IsSymbol('(')) depth++;
            else if (tokens[i].IsSymbol(')'))
            {
                depth--;
                if (depth == 0)
                {
                    definitions.Add(Define(sql, tokens, begin, i));
                    close = i;
                    break;
                }
            }
            else if (depth == 1 && tokens[i].IsSymbol(','))
            {
                definitions.Add(Define(sql, tokens, begin, i));
                begin = i + 1;
            }
        }
        if (close < 0) throw new InvalidOperationException("the stored table definition has an unbalanced column list.");
        return new ParsedTable(definitions, sql[tokens[close].End..].Trim());
    }

    private static TableDefinition Define(string sql, IReadOnlyList<SqlToken> tokens, int from, int to)
    {
        var own = tokens.Skip(from).Take(to - from).ToList();
        if (own.Count == 0) throw new InvalidOperationException("the stored table definition holds an empty definition.");
        var text = sql[own[0].Start..own[^1].End];
        var head = own[0];
        if (head.IsWord("CONSTRAINT"))
            return new TableDefinition(text, DefinitionKind.Constraint, own.Count > 1 ? own[1].Value : null, own);
        if (head.IsWord("PRIMARY") || head.IsWord("UNIQUE") || head.IsWord("CHECK") || head.IsWord("FOREIGN"))
            return new TableDefinition(text, DefinitionKind.Constraint, null, own);
        return new TableDefinition(text, DefinitionKind.Column, head.Value, own);
    }

    internal static bool IsForeignKey(TableDefinition definition)
    {
        var tokens = definition.Tokens;
        var start = definition.Name is null ? 0 : 2;
        return definition.Kind == DefinitionKind.Constraint && tokens.Count > start + 1
            && tokens[start].IsWord("FOREIGN") && tokens[start + 1].IsWord("KEY");
    }

    internal static ForeignKeyText ParseForeignKey(TableDefinition definition)
    {
        var tokens = definition.Tokens;
        var i = definition.Name is null ? 2 : 4;
        var columns = List(tokens, ref i);
        while (i < tokens.Count && !tokens[i].IsWord("REFERENCES")) i++;
        i++;
        var table = tokens[i++].Value;
        var referenced = i < tokens.Count && tokens[i].IsSymbol('(') ? List(tokens, ref i) : [];
        var onDelete = ReferentialAction.NoAction;
        var onUpdate = ReferentialAction.NoAction;
        for (; i < tokens.Count; i++)
        {
            if (!tokens[i].IsWord("ON") || i + 2 >= tokens.Count) continue;
            var action = Action(tokens, i + 2);
            if (tokens[i + 1].IsWord("DELETE")) onDelete = action;
            else if (tokens[i + 1].IsWord("UPDATE")) onUpdate = action;
        }
        return new ForeignKeyText(definition.Name, columns, table, referenced, onDelete, onUpdate);
    }

    internal static ForeignKeyText? TryParseForeignKey(TableDefinition definition)
    {
        try
        {
            return ParseForeignKey(definition);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    internal static ReferentialAction Action(IReadOnlyList<SqlToken> tokens, int at)
    {
        var first = tokens[at];
        if (first.IsWord("CASCADE")) return ReferentialAction.Cascade;
        if (first.IsWord("RESTRICT")) return ReferentialAction.Restrict;
        if (first.IsWord("NO")) return ReferentialAction.NoAction;
        if (first.IsWord("SET") && at + 1 < tokens.Count && tokens[at + 1].IsWord("NULL")) return ReferentialAction.SetNull;
        throw new NotSupportedException("the referential action in the stored table definition is not one the library models (it is SET DEFAULT or unknown).");
    }

    internal static ReferentialAction ActionOf(string text) => text.ToUpperInvariant() switch
    {
        "CASCADE" => ReferentialAction.Cascade,
        "RESTRICT" => ReferentialAction.Restrict,
        SqliteDataLiterals.NoAction => ReferentialAction.NoAction,
        SqliteDataLiterals.SetNull => ReferentialAction.SetNull,
        _ => throw new NotSupportedException($"the referential action '{text}' is not one the library models."),
    };

    private static List<string> List(IReadOnlyList<SqlToken> tokens, ref int i)
    {
        var names = new List<string>();
        while (i < tokens.Count && !tokens[i].IsSymbol('(')) i++;
        i++;
        for (; i < tokens.Count && !tokens[i].IsSymbol(')'); i++)
            if (tokens[i].Kind is SqlTokenKind.Word or SqlTokenKind.Identifier) names.Add(tokens[i].Value);
        i++;
        return names;
    }

    internal static bool MentionsInKeysOrPredicate(string indexSql, string column)
    {
        var depth = 0;
        var seenWhere = false;
        foreach (var token in Tokenize(indexSql))
        {
            if (token.IsSymbol('(')) depth++;
            else if (token.IsSymbol(')')) depth--;
            else if (depth == 0 && token.IsWord("WHERE")) seenWhere = true;
            else if ((depth > 0 || seenWhere) && token.Names(column)) return true;
        }
        return false;
    }

    internal static bool Mentions(string sql, string name) => Tokenize(sql).Any(t => t.Names(name));
}
