using System.Collections;
using System.Globalization;
using System.Text;

namespace Neemle.Orm.Lite.Tests.Corpus;

public static class CorpusFormat
{
    public static string Value(object? value) => value switch
    {
        null => "null",
        DBNull => "DBNull",
        string s => "String " + Quote(s),
        Guid g => "Guid " + g.ToString("D", CultureInfo.InvariantCulture),
        bool b => "Boolean " + (b ? "true" : "false"),
        DateTime d => "DateTime " + d.ToString("O", CultureInfo.InvariantCulture),
        DateOnly d => "DateOnly " + d.ToString("O", CultureInfo.InvariantCulture),
        Jsonb j => "Jsonb " + j.Json,
        Enum e => "Enum " + e.GetType().Name + "." + e,
        double d => "Double " + d.ToString("R", CultureInfo.InvariantCulture),
        float f => "Single " + f.ToString("R", CultureInfo.InvariantCulture),
        decimal m => "Decimal " + m.ToString(CultureInfo.InvariantCulture),
        IFormattable f when value.GetType().IsPrimitive => value.GetType().Name + " " + f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable items => Sequence(value.GetType(), items),
        _ => value.GetType().Name + " " + value,
    };

    private static string Sequence(Type type, IEnumerable items)
    {
        var rendered = new List<string>();
        foreach (var item in items) rendered.Add(Value(item));
        return TypeName(type) + " count=" + rendered.Count + " { " + string.Join(", ", rendered) + " }";
    }

    private static string TypeName(Type type)
    {
        if (type.IsArray) return TypeName(type.GetElementType()!) + "[]";
        if (!type.IsGenericType) return type.Name;
        var name = type.Name[..type.Name.IndexOf('`')];
        return name + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
    }

    public static string Quote(string text)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in text)
            sb.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        return sb.Append('"').ToString();
    }

    public static string Statement(string sql, IReadOnlyList<object?> parameters)
    {
        var sb = new StringBuilder();
        sb.Append("-- sql\n").Append(sql).Append('\n');
        sb.Append("-- parameters (").Append(parameters.Count).Append(")\n");
        for (var i = 0; i < parameters.Count; i++)
            sb.Append('[').Append(i).Append("] ").Append(Value(parameters[i])).Append('\n');
        return sb.ToString();
    }

    public static string Lines(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        var index = 0;
        foreach (var line in lines)
            sb.Append('[').Append(index++).Append("] ").Append(line).Append('\n');
        return sb.ToString();
    }

    public static string Refusal(Exception exception) =>
        "-- refusal\ntype: " + exception.GetType().FullName + "\nmessage: " + exception.Message + "\n";

    public static string Section(string title, string body) => "-- " + title + "\n" + body + (body.EndsWith('\n') ? "" : "\n");
}
