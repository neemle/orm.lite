using System.Text;

namespace Neemle.OrmLite;

internal sealed class SchemaSourceBuilder
{
    private readonly List<string> _methods = [];
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    internal string Expression(string type, string expression)
    {
        var key = type + " " + expression;
        if (_cache.TryGetValue(key, out var existing)) return existing;
        var name = "Value" + _methods.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var method = "    private static " + type + " " + name + "() =>\n        " + expression + ";\n";
        _cache.Add(key, name + "()"); _methods.Add(method);
        return name + "()";
    }

    internal string Call(string type, string call, params string[] arguments) =>
        Expression(type, Pack(call + "(", arguments, ")"));

    internal string List(string type, IEnumerable<string> values) =>
        Expression("G.IReadOnlyList<" + type + ">",
            Pack("global::System.Array.AsReadOnly(new " + type + "[] {", values, "})"));

    private static string Pack(string prefix, IEnumerable<string> values, string suffix)
    {
        var lines = new List<string>(); var current = prefix;
        foreach (var value in values)
        {
            if (current.Length + value.Length + 2 > 100) { lines.Add(current.TrimEnd()); current = "    "; }
            current += value + ", ";
        }
        if (current.EndsWith(", ", StringComparison.Ordinal)) current = current[..^2];
        lines.Add(current + suffix);
        return string.Join("\n            ", lines);
    }

    internal string Text(string? value)
    {
        if (value is null) return "null";
        var chunks = new List<string>(); var literal = new StringBuilder();
        foreach (var character in value)
        {
            var escaped = character is >= ' ' and <= '~' and not ('"' or '\\') ? character.ToString()
                : "\\u" + ((int)character).ToString("x4", System.Globalization.CultureInfo.InvariantCulture);
            if (literal.Length + escaped.Length > 70) { chunks.Add("\"" + literal + "\""); literal.Clear(); }
            literal.Append(escaped);
        }
        chunks.Add("\"" + literal + "\"");
        return chunks.Count == 1 ? chunks[0] : Expression("string", string.Join(" +\n            ", chunks));
    }

    internal string Finish(SchemaDiffIdentity identity, string recipe)
    {
        var name = string.Join(".", identity.CSharpNamespace.Split('.').Select(p => "@" + p));
        var text = "#nullable enable\nusing R = global::Neemle.OrmLite;\nusing N = global::Neemle.OrmLite.Nodes;\n"
            + "using G = global::System.Collections.Generic;\n\nnamespace "
            + name + ";\n\n"
            + "// Down drops newly added columns/tables and loses post-Up data. Backup is required to retain that data.\n"
            + "// DownProofRequired is an obligation: establish actual Up/Down/Up fixture evidence before acceptance.\n"
            + "// Comparison proves supported physical schema facts, not historical logical-domain intent.\n"
            + "public static class @" + identity.CSharpTypeName + "\n{\n"
            + "    public static R.SchemaDiffArtifact Create() => R.SchemaDiffArtifact.FromRecipe(" + recipe + ");\n\n"
            + string.Join("\n", _methods) + "}\n";
        var lines = text.Split('\n');
        if (lines.Length > 490 || lines.Any(line => line.Length > 120))
            throw new SchemaDiffException(SchemaDiffError.LimitExceeded);
        return text;
    }
}
