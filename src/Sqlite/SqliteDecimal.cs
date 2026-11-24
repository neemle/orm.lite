using System.Globalization;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Nodes;
using SQLitePCL;
using static Neemle.OrmLite.Sqlite.SqliteEngineValues;

namespace Neemle.OrmLite.Sqlite;

internal static class SqliteDecimal
{
    internal const string Collation = "orm_decimal";

    internal const string FitsFunction = "orm_decimal_fits";

    internal const string SumFunction = "orm_decimal_sum";

    internal const string AverageFunction = "orm_decimal_average";

    internal const string ZeroText = "0";

    internal const string CollateKeyword = "COLLATE";

    internal const int MaxPrecision = 28;

    private const NumberStyles Style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    internal static bool Accepts(TypeRef type) =>
        type.Precision <= MaxPrecision && type.Scale >= 0 && (type.Precision == 0 ? type.Scale == 0 : type.Scale <= type.Precision);

    internal static NotSupportedException Refusal(TypeRef type) =>
        new NotSupportedException($"a decimal column of precision {type.Precision} and scale {type.Scale} is not one provider 'sqlite' stores exactly: the precision is at most {MaxPrecision} digits and the scale is 0 to the precision.");

    internal static NotSupportedException CastRefusal() =>
        new NotSupportedException("SQLite has no exact decimal cast: CAST AS real would round through a double, so provider 'sqlite' refuses it. Compare and store the decimal as it is.");

    internal static void Register(SqliteConnection connection, sqlite3 handle)
    {
        connection.CreateCollation(Collation, Compare);
        SqliteEngineFunctions.Scalar(handle, FitsFunction, 3, a => Fits(TextOf(a[0]), LongOf(a[1]), LongOf(a[2])));
        new SqliteEngineAggregate<decimal?>(null, (total, a) => Add(total, TextOf(a[0])), total => Text(total))
            .Register(handle, SumFunction, 1);
        new SqliteEngineAggregate<(decimal Sum, long Count, long Scale)>(
                (0m, 0L, 0L), (state, a) => Accumulate(state, TextOf(a[0]), LongOf(a[1])), state => Average(state))
            .Register(handle, AverageFunction, 2);
    }

    private static (decimal Sum, long Count, long Scale) Accumulate((decimal Sum, long Count, long Scale) state, string? value, long scale) =>
        value is null
            ? (state.Sum, state.Count, scale)
            : (state.Sum + decimal.Parse(value, Style, CultureInfo.InvariantCulture), state.Count + 1, scale);

    private static string? Average((decimal Sum, long Count, long Scale) state) =>
        state.Count == 0
            ? null
            : Math.Round(state.Sum / state.Count, (int)state.Scale, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

    internal static int Compare(string left, string right) =>
        decimal.TryParse(left, Style, CultureInfo.InvariantCulture, out var a) && decimal.TryParse(right, Style, CultureInfo.InvariantCulture, out var b)
            ? a.CompareTo(b)
            : string.CompareOrdinal(left, right);

    internal static long? Fits(string? text, long precision, long scale)
    {
        if (text is null) return null;
        if (!decimal.TryParse(text, Style, CultureInfo.InvariantCulture, out _)) return 0;
        if (precision <= 0) return 1;
        var digits = text.TrimStart('+', '-');
        var point = digits.IndexOf('.', StringComparison.Ordinal);
        var whole = (point < 0 ? digits : digits[..point]).TrimStart('0');
        var fraction = point < 0 ? "" : digits[(point + 1)..].TrimEnd('0');
        return whole.Length <= precision - scale && fraction.Length <= scale ? 1 : 0;
    }

    private static decimal? Add(decimal? total, string? value) =>
        value is null ? total : (total ?? 0m) + decimal.Parse(value, Style, CultureInfo.InvariantCulture);

    private static string? Text(decimal? total) => total?.ToString(CultureInfo.InvariantCulture);
}
