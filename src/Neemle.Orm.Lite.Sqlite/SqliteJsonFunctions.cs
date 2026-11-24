using System.Globalization;
using SQLitePCL;
using static Neemle.Orm.Lite.Sqlite.SqliteEngineValues;

namespace Neemle.Orm.Lite.Sqlite;

internal static class SqliteJsonFunctions
{
    internal const string Normalize = "orm_json";

    internal const string KeyText = "orm_json_text";

    internal const string HasKey = "orm_json_has";

    internal const string ArrayHas = "orm_json_array_has";

    internal const string Concat = "orm_json_concat";

    internal const string Remove = "orm_json_remove";

    internal const string ObjectOf = "orm_json_object";

    internal const string IntOrZero = "orm_json_int";

    internal const string Number = "orm_json_number";

    internal const string Double = "orm_json_double";

    internal const string Boolean = "orm_json_boolean";

    internal const string EpochMillis = "orm_epoch_millis";

    internal static void Register(sqlite3 handle)
    {
        Add(handle, Normalize, 1, a => NormalizeText(TextOf(a[0])));
        Add(handle, KeyText, 2, a => ExtractText(TextOf(a[0]), TextOf(a[1])));
        Add(handle, HasKey, 2, a => KeyExists(TextOf(a[0]), TextOf(a[1])));
        Add(handle, ArrayHas, 3, a => ArrayContains(TextOf(a[0]), TextOf(a[1]), TextOf(a[2])));
        Add(handle, Concat, 2, a => ConcatDocuments(TextOf(a[0]), TextOf(a[1])));
        Add(handle, Remove, 2, a => RemoveKey(TextOf(a[0]), TextOf(a[1])));
        Add(handle, ObjectOf, 2, a => BuildObject(TextOf(a[0]), ValueOf(a[1])));
        Add(handle, IntOrZero, 1, a => IntegerOrZero(ValueOf(a[0])));
        Add(handle, Number, 1, a => NumericValue(ValueOf(a[0])));
        Add(handle, Double, 1, a => DoubleValue(ValueOf(a[0])));
        Add(handle, Boolean, 1, a => BooleanValue(ValueOf(a[0])));
        Add(handle, EpochMillis, 1, a => EpochMilliseconds(TextOf(a[0])));
    }

    private static void Add(sqlite3 handle, string name, int arity, Func<sqlite3_value[], object?> body) =>
        SqliteEngineFunctions.Scalar(handle, name, arity, body);

    internal static object? NormalizeText(string? text) =>
        text is null ? null : SqliteJsonModel.Write(SqliteJsonModel.Parse(text));

    internal static object? ExtractText(string? document, string? key)
    {
        if (document is null || key is null) return null;
        return SqliteJsonModel.Parse(document) is JsonObjectNode root && root.Find(key) is { } member
            ? SqliteJsonModel.Text(member)
            : null;
    }

    internal static object? KeyExists(string? document, string? key)
    {
        if (document is null || key is null) return null;
        var found = SqliteJsonModel.Parse(document) switch
        {
            JsonObjectNode root => root.Find(key) is not null,
            JsonArrayNode array => array.Items.Any(item => item is JsonStringNode s && s.Text == key),
            JsonStringNode scalar => scalar.Text == key,
            _ => false,
        };
        return found ? 1L : 0L;
    }

    internal static object? ArrayContains(string? document, string? key, string? value)
    {
        if (document is null || key is null) return null;
        if (SqliteJsonModel.Parse(document) is not JsonObjectNode root || root.Find(key) is not { } member) return null;
        if (member is not JsonArrayNode array) return 0L;
        if (value is null) return null;
        return array.Items.Any(item => item is JsonStringNode s && s.Text == value) ? 1L : 0L;
    }

    internal static object? ConcatDocuments(string? left, string? right)
    {
        if (left is null || right is null) return null;
        var a = SqliteJsonModel.Parse(left);
        var b = SqliteJsonModel.Parse(right);
        if (a is JsonObjectNode x && b is JsonObjectNode y)
        {
            var merged = new Dictionary<string, JsonValueNode>(StringComparer.Ordinal);
            foreach (var member in x.Members) merged[member.Key] = member.Value;
            foreach (var member in y.Members) merged[member.Key] = member.Value;
            return SqliteJsonModel.Write(SqliteJsonModel.Sorted(merged));
        }
        var items = new List<JsonValueNode>();
        items.AddRange(a is JsonArrayNode first ? first.Items : [a]);
        items.AddRange(b is JsonArrayNode second ? second.Items : [b]);
        return SqliteJsonModel.Write(new JsonArrayNode(items));
    }

    internal static object? RemoveKey(string? document, string? key)
    {
        if (document is null || key is null) return null;
        switch (SqliteJsonModel.Parse(document))
        {
            case JsonObjectNode root:
                return SqliteJsonModel.Write(new JsonObjectNode(root.Members.Where(m => m.Key != key).ToList()));
            case JsonArrayNode array:
                return SqliteJsonModel.Write(new JsonArrayNode(array.Items.Where(i => i is not JsonStringNode s || s.Text != key).ToList()));
            default:
                throw new InvalidOperationException("cannot delete from scalar");
        }
    }

    internal static object? BuildObject(string? key, object? value)
    {
        if (key is null) throw new ArgumentException("null value not allowed for object key");
        JsonValueNode node = Plain(value) switch
        {
            null => JsonNullNode.Instance,
            long whole => new JsonNumberNode(whole.ToString(CultureInfo.InvariantCulture)),
            double real when double.IsFinite(real) => new JsonNumberNode(SqliteJsonModel.CanonicalNumber(real.ToString("R", CultureInfo.InvariantCulture))),
            double real => new JsonStringNode(double.IsNegative(real) ? "-Infinity" : "Infinity"),
            string text => new JsonStringNode(text),
            _ => throw new InvalidOperationException("could not determine the JSON type of the value"),
        };
        return SqliteJsonModel.Write(new JsonObjectNode([new KeyValuePair<string, JsonValueNode>(key, node)]));
    }

    internal static object? IntegerOrZero(object? value)
    {
        var text = AsText(value);
        return text is null || text.Length == 0 ? 0L : (long)SqliteJsonInput.Int32(text);
    }

    internal static object? NumericValue(object? value) => Plain(value) switch
    {
        null => null,
        long whole => (double)whole,
        double real => double.IsNaN(real) ? null : real,
        _ => AsText(value) is { } text ? SqliteJsonInput.Numeric(text) : null,
    };

    internal static object? DoubleValue(object? value) => Plain(value) switch
    {
        null => null,
        long whole => (double)whole,
        double real => double.IsNaN(real) ? null : real,
        _ => AsText(value) is { } text ? SqliteJsonInput.Float8(text) : null,
    };

    internal static object? BooleanValue(object? value) =>
        AsText(value) is { } text && SqliteJsonInput.Boolean(text) is { } flag ? (flag ? 1L : 0L) : null;

    internal static object? EpochMilliseconds(string? text)
    {
        if (text is null) return null;
        if (!DateTimeOffset.TryParse(
                text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment))
            throw new FormatException($"invalid input syntax for type timestamp with time zone: \"{text}\"");
        var ticks = moment.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks;
        var microseconds = Math.Round(ticks / 10m, MidpointRounding.ToEven);
        return (double)Math.Floor(microseconds / 1000m);
    }

    private static object? Plain(object? value) => value is DBNull ? null : value;

    private static string? AsText(object? value) => Plain(value) switch
    {
        null => null,
        string text => text,
        long whole => whole.ToString(CultureInfo.InvariantCulture),
        double real => real.ToString("R", CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("a BLOB cannot be read as JSON input text"),
    };
}
