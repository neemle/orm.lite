using System.Globalization;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class DdlLiterals
{
    internal static Node Of(object? value) => Render(ParamLiterals.Normalise(value));

    internal static NodeList Columns(IEnumerable<string> names) =>
        NodeList.Of(names.Select(n => (Node)new ColumnRef(n)));

    private static Node Render(object? value) => value switch
    {
        null => new NullLit(),
        string text => new StringLit(Clean(text)),
        char symbol => new StringLit(Clean(symbol.ToString())),
        bool flag => new BoolLit(flag),
        sbyte or byte or short or ushort or int or uint or long =>
            new IntLit(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
        _ => Number(value),
    };

    private static string Clean(string value) => NoLoneSurrogate(NoNul(value));

    private static string NoNul(string value) => value.Contains('\0')
        ? throw new ArgumentException(
            "a string or character value holds a NUL character (U+0000), which no SQL text literal can carry; "
            + "remove it from the value.",
            nameof(value))
        : value;

    private static string NoLoneSurrogate(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) i++;
            else if (char.IsSurrogate(value[i]))
                throw new ArgumentException(
                    "a string or character value holds half of a surrogate pair (a lone surrogate), which is not "
                    + "valid text and has no SQL text literal; remove it from the value.",
                    nameof(value));
        }
        return value;
    }

    private static BareLit Number(object value) => value switch
    {
        float number when number == 0 => new BareLit("0"),
        double number when number == 0 => new BareLit("0"),
        ulong number => new BareLit(number.ToString(CultureInfo.InvariantCulture)),
        decimal number => new BareLit(number.ToString(CultureInfo.InvariantCulture), ExactDecimal: true),
        float number when float.IsFinite(number) => new BareLit(number.ToString(CultureInfo.InvariantCulture)),
        double number when double.IsFinite(number) => new BareLit(number.ToString(CultureInfo.InvariantCulture)),
        float or double => throw new ArgumentException(
            string.Create(CultureInfo.InvariantCulture, $"{value} has no portable literal; write a finite number."),
            nameof(value)),
        _ => throw new NotSupportedException(
            $"{TypeName(value.GetType())} has no literal form; write a string, a number, "
            + "a flag, a character, a Guid, a date or time, a Jsonb or an enum member."),
    };

    // A generic type's FullName spells every argument assembly-qualified; a refusal names it readably instead.
    private static string TypeName(Type type)
    {
        if (!type.IsGenericType) return type.FullName ?? type.Name;
        var name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];
        var arguments = string.Join(", ", type.GetGenericArguments().Select(TypeName));
        return (type.Namespace is null ? "" : type.Namespace + ".") + name + "<" + arguments + ">";
    }
}
