#nullable enable
using System;
using System.Globalization;

namespace Neemle.Orm.Lite;

internal static class ScalarValue
{
    internal const NumberStyles DecimalStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    internal static TCol To<TCol>(object? raw, string table, string column)
    {
        if (raw is null or DBNull) return default!;
        if (raw is TCol same) return same;
        object? made;
        try { made = Made(raw, Underlying(typeof(TCol)), table, column); }
        catch (OverflowException) { throw Overflow<TCol>(raw); }
        return made is TCol converted ? converted : throw Refusal<TCol>(raw);
    }

    private static object? Made(object raw, Type target, string table, string column)
    {
        if (target.IsEnum) return raw is string name ? Member(target, name, table, column) : null;
        var code = Type.GetTypeCode(target);
        if (IsInteger(raw)) return FromInteger(raw, target, code);
        if (IsFloating(raw)) return code is TypeCode.Single or TypeCode.Double ? Narrowed(raw, target) : null;
        return raw is string text ? FromText(text, target, code) : null;
    }

    private static object? FromInteger(object raw, Type target, TypeCode code)
    {
        if (code == TypeCode.Boolean) return Flag(raw);
        return code is >= TypeCode.SByte and <= TypeCode.Decimal ? Narrowed(raw, target) : null;
    }

    // ORML-73: a finite double too large for a float comes out of ChangeType as Infinity, which the column never
    // held. It is an overflow like any other narrowing; a NaN or an infinity that WAS stored is read as it was.
    private static object Narrowed(object raw, Type target)
    {
        var made = Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
        if (made is float single && !float.IsFinite(single) && IsFinite(raw)) throw new OverflowException();
        return made;
    }

    private static bool IsFinite(object raw) => raw is not double number || double.IsFinite(number);

    private static object? Flag(object raw)
    {
        var number = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
        if (number == 0m) return false;
        if (number == 1m) return true;
        return null;
    }

    private static object? FromText(string text, Type target, TypeCode code)
    {
        var culture = CultureInfo.InvariantCulture;
        if (code == TypeCode.Decimal)
            return Boxed(decimal.TryParse(text, DecimalStyle, culture, out var number), number);
        if (code == TypeCode.DateTime) return Moment(text, culture);
        if (target == typeof(Guid)) return Boxed(Guid.TryParse(text, out var id), id);
        if (target == typeof(DateOnly))
            return Boxed(DateOnly.TryParse(text, culture, DateTimeStyles.None, out var day), day);
        return target == typeof(Jsonb) ? Jsonb.FromStorage(text) : null;
    }

    // ORML-74: Z text is Kind Utc and zone-less text is Kind Unspecified (RoundtripKind). Text with a numeric offset
    // would come back as Kind Local, converted to the machine's zone; it is read as the same instant in Kind Utc, which
    // is what the row readers give, so the two read paths agree for it and the value does not depend on the machine.
    private static object? Moment(string text, CultureInfo culture)
    {
        if (!DateTime.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var moment)) return null;
        if (moment.Kind != DateTimeKind.Local) return moment;
        var universal = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        return DateTime.TryParse(text, culture, universal, out var instant) ? instant : null;
    }

    private static object? Boxed<T>(bool parsed, T value) where T : struct => parsed ? (object)value : null;

    private static object Member(Type target, string text, string table, string column)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > 0
            && Enum.TryParse(target, trimmed, true, out var parsed)
            && parsed is not null
            && Enum.IsDefined(target, parsed)
            && string.Equals(parsed.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
            return parsed;
        throw new OrmEnumValueException(table, column, "", text, Enum.GetNames(target));
    }

    private static Type Underlying(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static bool IsInteger(object raw) =>
        raw is sbyte or byte or short or ushort or int or uint or long or ulong;

    private static bool IsFloating(object raw) => raw is float or double;

    private static OverflowException Overflow<TCol>(object raw) =>
        new("ScalarAsync<" + Name(typeof(TCol)) + "> cannot hold the " + raw.GetType().Name
            + " the query returned because it is outside the range of " + Underlying(typeof(TCol)).Name);

    private static InvalidCastException Refusal<TCol>(object raw) =>
        new("ScalarAsync<" + Name(typeof(TCol)) + "> cannot convert the " + raw.GetType().Name
            + " the query returned; " + Why(raw, Underlying(typeof(TCol))));

    private static string Why(object raw, Type target)
    {
        var code = Type.GetTypeCode(target);
        if (IsFloating(raw) && code == TypeCode.Decimal)
            return "a decimal is never rounded through a floating point number, so read a decimal column as a decimal";
        if (IsInteger(raw) && code == TypeCode.Boolean) return "only the integers 0 and 1 are a bool";
        if (raw is string
            && (code is TypeCode.Decimal or TypeCode.DateTime || target == typeof(Guid) || target == typeof(DateOnly)))
            return "the text is not a " + target.Name + " in the form the library writes";
        return "no conversion between these types is defined";
    }

    private static string Name(Type type) =>
        Nullable.GetUnderlyingType(type) is { } inner ? inner.Name + "?" : type.Name;
}
