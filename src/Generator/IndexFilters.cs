using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CondDef = Neemle.OrmLite.Generator.OrmGenerator.CondDef;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Reads the Where text of an [Index]: conditions joined by and, each a column compared with a text, a whole number
/// or a flag, or a column that is null or is not null. A condition that cannot be read leaves a problem text.
/// </summary>
internal static class IndexFilters
{
    private static readonly Regex AndSeparator = new Regex(
        @"\s+and\s+(?=(?:[^']*'[^']*')*[^']*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Condition = new Regex(
        @"^\s*(?<n>\w+)\s*(?:(?<nul>is\s+not\s+null|is\s+null)|"
        + @"(?<op><=|>=|<>|<|>|=)\s*(?<v>'(?:[^']|'')*'|-?\d+|true|false))\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static ImmutableArray<CondDef> Parse(string? text, List<Prop> props, List<string> problems)
    {
        if (text is null) return [];
        var conditions = new List<CondDef>();
        foreach (var part in AndSeparator.Split(text))
            if (ParseCondition(part, props, problems) is { } condition) conditions.Add(condition);
        return [.. conditions];
    }

    private static CondDef? ParseCondition(string part, List<Prop> props, List<string> problems)
    {
        var m = Condition.Match(part);
        if (!m.Success)
        {
            problems.Add(IndexInputs.Unreadable(part));
            return null;
        }
        if (IndexTerms.Resolve(props, m.Groups["n"].Value, problems) is not { } prop) return null;
        if (!m.Groups["nul"].Success) return Compared(m, prop, problems);
        var test = m.Groups["nul"].Value.ToLowerInvariant().Contains("not") ? "NotNull" : "IsNull";
        return new CondDef(prop.Column, test, "", "");
    }

    private static CondDef? Compared(Match m, Prop prop, List<string> problems)
    {
        var literal = m.Groups["v"].Value;
        var kind = literal[0] == '\'' ? "string" : literal.ToLowerInvariant() is "true" or "false" ? "bool" : "int";
        if (!Fits(kind, prop))
        {
            problems.Add("'" + prop.Name + "' is " + prop.Cs + " and cannot be compared with " + literal);
            return null;
        }
        if (kind == "int" && IndexInputs.NumberProblem(literal) is { } outOfRange)
        {
            problems.Add(outOfRange);
            return null;
        }
        var value = kind == "string"
            ? literal.Substring(1, literal.Length - 2).Replace("''", "'")
            : literal.ToLowerInvariant();
        return new CondDef(prop.Column, OperatorName(m.Groups["op"].Value), kind, value);
    }

    private static bool Fits(string kind, Prop prop) => kind switch
    {
        "string" => prop.Read is "String" or "Enum",
        "bool" => prop.Read == "Boolean",
        _ => prop.Read is "Int32" or "Int64" or "Double" or "Decimal",
    };

    private static string OperatorName(string symbol) => symbol switch
    {
        "=" => "Equal",
        "<>" => "NotEqual",
        "<" => "Less",
        "<=" => "LessOrEqual",
        ">" => "Greater",
        _ => "GreaterOrEqual",
    };
}
