using System.Globalization;
using System.Text;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class PostgresJsonAgreementTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly string[] Keys = ["a", "b", "aa", "é", "ключ", "k\"", "a.b", "", "$x", "zz", "😀", "kind", "n"];

    private static string Literal(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string TextArray(IEnumerable<string> values) => "ARRAY[" + string.Join(",", values.Select(Literal)) + "]::text[]";

    private static string RandomNumber(Random r)
    {
        var text = new StringBuilder();
        if (r.Next(4) == 0) text.Append('-');
        text.Append(r.Next(3) == 0 ? "0" : r.Next(1, 10).ToString(CultureInfo.InvariantCulture) + new string('0', r.Next(0, 4)) + r.Next(0, 1000).ToString(CultureInfo.InvariantCulture));
        if (r.Next(2) == 0)
        {
            text.Append('.');
            for (var i = r.Next(1, 6); i > 0; i--) text.Append((char)('0' + (r.Next(3) == 0 ? 0 : r.Next(10))));
        }
        if (r.Next(3) == 0)
        {
            text.Append(r.Next(2) == 0 ? 'e' : 'E');
            if (r.Next(2) == 0) text.Append(r.Next(2) == 0 ? '+' : '-');
            text.Append(r.Next(0, 40).ToString(CultureInfo.InvariantCulture));
        }
        return text.ToString();
    }

    private static string RandomString(Random r)
    {
        string palette = "abcXYZ019 _-./\"\\\t\n\r\b\f" + (char)1 + (char)0x1f + (char)0x7f + (char)0xe9 + (char)0x436 + (char)0x406 + (char)0x2028 + char.ConvertFromUtf32(0x1F600) + "<>&'";
        var text = new StringBuilder("\"");
        for (var i = r.Next(0, 8); i > 0; i--)
        {
            var at = r.Next(palette.Length);
            if (char.IsHighSurrogate(palette[at]) || char.IsLowSurrogate(palette[at]))
            {
                text.Append(r.Next(2) == 0 ? "😀" : "\\ud83d\\ude00");
                continue;
            }
            var c = palette[at];
            switch (c)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\t': text.Append(r.Next(2) == 0 ? "\\t" : "\\u0009"); break;
                case '\n': text.Append(r.Next(2) == 0 ? "\\n" : "\\u000a"); break;
                case '\r': text.Append("\\r"); break;
                case '\b': text.Append("\\b"); break;
                case '\f': text.Append("\\f"); break;
                case '/': text.Append(r.Next(2) == 0 ? "/" : "\\/"); break;
                default:
                    if (c < ' ') text.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    else if (c > '~' && r.Next(3) == 0) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else text.Append(c);
                    break;
            }
        }
        return text.Append('"').ToString();
    }

    private static string RandomKey(Random r) => Quote(Keys[r.Next(Keys.Length)], r);

    private static string Quote(string key, Random r)
    {
        var text = new StringBuilder("\"");
        foreach (var c in key)
        {
            if (c == '"') text.Append("\\\"");
            else if (c > '~' && r.Next(3) == 0 && !char.IsSurrogate(c)) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else text.Append(c);
        }
        return text.Append('"').ToString();
    }

    private static string RandomValue(Random r, int depth)
    {
        switch (r.Next(depth > 3 ? 5 : 8))
        {
            case 0: return "null";
            case 1: return r.Next(2) == 0 ? "true" : "false";
            case 2: return RandomNumber(r);
            case 3:
            case 4: return RandomString(r);
            case 5:
            case 6:
                return "[" + string.Join(r.Next(2) == 0 ? "," : " , ", Enumerable.Range(0, r.Next(0, 4)).Select(_ => RandomValue(r, depth + 1))) + "]";
            default:
                return "{" + string.Join(",", Enumerable.Range(0, r.Next(0, 5)).Select(_ => RandomKey(r) + ":" + RandomValue(r, depth + 1))) + "}";
        }
    }

    private static List<string> Documents(int count, int seed)
    {
        var r = new Random(seed);
        var docs = new List<string>
        {
            "null", "true", "5", "\"kind\"", "[]", "{}", "[\"kind\"]", "{\"kind\":null}", "{\"a\":1,\"a\":2}", "[1,\"a\",[\"a\"],{\"a\":1}]",
            "1e400", "-0", "-0.0", "0.00e2", "1.50e1", "12.0e1", "1E-3", "0e5", "{\"k\":[\"x\",\"y\"]}", "{\"n\":\"\"}",
        };
        for (var i = 0; i < count; i++) docs.Add(r.Next(3) == 0 ? RandomValue(r, 0) : "{" + RandomKey(r) + ":" + RandomValue(r, 1) + "," + RandomKey(r) + ":" + RandomValue(r, 1) + "}");
        return docs;
    }

    [SkippableFact(DisplayName = "the emulated input grammar accepts exactly the texts PostgreSQL accepts as numeric, double precision, boolean and integer, and reads the same value from each, over a fixed corpus and 4000 seeded random strings")]
    public async Task Input_grammar_agrees()
    {
        fixture.RequireAvailable();
        var r = new Random(20260401);
        const string alphabet = "0123456789+-._eExXbBoOaAfFNnIi \t";
        var strings = new List<string>
        {
            "", " ", "1", "-1", "+1", "1.5", ".5", "5.", ".", "1e2", "1E+2", "1e-2", "1e", "e1", "0x1F", "0X1f", "0x", "0x_1F", "0b11", "0o17", "0b2", "1_000", "1__0", "_1", "1_", "1_000.000_1", "1._5", "1.5_", "1e1_0",
            "NaN", "nan", "NAN", "+nan", "Infinity", "inf", "-inf", "+Infinity", "-Infinity", "infinit", "infinityx", " 12 ", "\t12\n", "12abc", "1 2", "--1", "+-1", "٣", "1,5", "0.1", "00012", "-0", "2147483647", "2147483648", "-2147483648", "-2147483649", "99999999999",
            "true", "false", "t", "f", "tr", "fa", "yes", "no", "y", "n", "on", "off", "of", "o", "1", "0", "TRUE", " T ", "truee", "nope", "yess", "ye", "fals", "oN", "OFF", "0x1", "2", "-1", "01",
            "1e308", "1e309", "1e-310", "1e-400", "0e500", "0.0000", "123456789012345678901234567890", "1e1000", "0x1.8p3", "0x10", "-0x10", "1d2", "1f", ".e1", "+.5e-3",
        };
        for (var i = 0; i < 4000; i++)
            strings.Add(new string(Enumerable.Range(0, r.Next(1, 9)).Select(_ => alphabet[r.Next(alphabet.Length)]).ToArray()));
        strings = strings.Distinct(StringComparer.Ordinal).ToList();

        var rows = await fixture.RowsAsync(
            "SELECT s, pg_input_is_valid(s,'numeric'), pg_input_is_valid(s,'double precision'), pg_input_is_valid(s,'boolean'), pg_input_is_valid(s,'integer'), "
            + "CASE WHEN pg_input_is_valid(s,'integer') THEN s::integer END, CASE WHEN pg_input_is_valid(s,'boolean') THEN s::boolean END "
            + "FROM unnest(" + TextArray(strings) + ") AS t(s)");

        var problems = new List<string>();
        foreach (var row in rows)
        {
            var s = (string)row[0]!;
            var numericValid = (bool)row[1]!;
            var floatValid = (bool)row[2]!;
            var booleanValid = (bool)row[3]!;
            var integerValid = (bool)row[4]!;
            var isNan = s.Trim().Equals("nan", StringComparison.OrdinalIgnoreCase);
            var signedNan = s.Trim().TrimStart('+', '-').Equals("nan", StringComparison.OrdinalIgnoreCase);
            var numeric = SqliteJsonInput.Numeric(s);
            var float8 = SqliteJsonInput.Float8(s);
            var flag = SqliteJsonInput.Boolean(s);
            if (numericValid != (numeric is not null || isNan)) problems.Add($"numeric {Show(s)}: postgres {numericValid}, emulated {numeric}");
            if (floatValid != (float8 is not null || signedNan) && !s.Contains("0x", StringComparison.OrdinalIgnoreCase)) problems.Add($"double {Show(s)}: postgres {floatValid}, emulated {float8}");
            if (booleanValid != (flag is not null)) problems.Add($"boolean {Show(s)}: postgres {booleanValid}, emulated {flag}");
            if (booleanValid && (bool)row[6]! != flag) problems.Add($"boolean value {Show(s)}");
            int? integer;
            try
            {
                integer = SqliteJsonInput.Int32(s);
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                integer = null;
            }
            if (integerValid != (integer is not null)) problems.Add($"integer {Show(s)}: postgres {integerValid}, emulated {integer}");
            if (integerValid && integer is not null && Convert.ToInt32(row[5], CultureInfo.InvariantCulture) != integer) problems.Add($"integer value {Show(s)}");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(40)));
    }

    private static string Show(string text) => "\"" + text.Replace("\t", "\\t", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    [SkippableFact(DisplayName = "numeric and double input read the same value as PostgreSQL for every text both accept")]
    public async Task Numeric_values_agree()
    {
        fixture.RequireAvailable();
        string[] numbers =
        [
            "1", "-1", "+1.5", ".5", "5.", "1e2", "1E-2", "0x1F", "-0x10", "0b101", "0o17", "1_000", "1_000.5", " 12 ", "00012", "0.1", "123456789012345678901234567890", "1e300", "-1.5e-300", "0e500", "9007199254740993", "0.30000000000000004",
        ];

        var rows = await fixture.RowsAsync(
            "SELECT s, s::numeric::double precision, CASE WHEN pg_input_is_valid(s,'double precision') THEN s::double precision END "
            + "FROM unnest(" + TextArray(numbers) + ") AS t(s)");

        foreach (var row in rows)
        {
            var s = (string)row[0]!;
            Assert.Equal(Convert.ToDouble(row[1], CultureInfo.InvariantCulture), SqliteJsonInput.Numeric(s));
            if (row[2] is not null) Assert.Equal(Convert.ToDouble(row[2], CultureInfo.InvariantCulture), SqliteJsonInput.Float8(s));
        }
    }

    [SkippableFact(DisplayName = "the canonical document text, a key read, key existence, array membership, concatenation and key removal equal PostgreSQL's over 1500 seeded random documents")]
    public async Task Document_operations_agree()
    {
        fixture.RequireAvailable();
        var docs = Documents(1500, 20260402);
        var others = Documents(1500, 20260403).Take(docs.Count).ToList();
        var r = new Random(20260404);
        var keys = docs.Select(_ => Keys[r.Next(Keys.Length)]).ToList();

        var rows = await fixture.RowsAsync(
            "WITH t AS (SELECT a::jsonb AS a, b::jsonb AS b, k FROM unnest(" + TextArray(docs) + ", " + TextArray(others) + ", " + TextArray(keys) + ") AS u(a, b, k)) "
            + "SELECT a::text, b::text, k, a ->> k, jsonb_exists(a, k), "
            + "CASE WHEN jsonb_typeof(a) = 'object' THEN jsonb_typeof(a -> k) = 'array' AND jsonb_exists(a -> k, 'x') END, "
            + "CASE WHEN jsonb_typeof(a) = 'object' THEN jsonb_typeof(a -> k) = 'array' END, "
            + "(a || b)::text, CASE WHEN jsonb_typeof(a) IN ('object','array') THEN (a - k)::text END "
            + "FROM t");

        var problems = new List<string>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var source = docs[i];
            var other = others[i];
            var key = (string)row[2]!;
            var canonical = SqliteJsonFunctions.NormalizeText(source);
            if ((string?)canonical != (string)row[0]!) problems.Add($"canonical {source}\n  postgres {row[0]}\n  emulated {canonical}");
            if ((string?)SqliteJsonFunctions.NormalizeText(other) != (string)row[1]!) problems.Add($"canonical {other}");
            if ((string?)SqliteJsonFunctions.ExtractText(source, key) != (string?)row[3]) problems.Add($"->> {source} {key}: postgres {row[3]} emulated {SqliteJsonFunctions.ExtractText(source, key)}");
            if ((long)SqliteJsonFunctions.KeyExists(source, key)! != ((bool)row[4]! ? 1L : 0L)) problems.Add($"exists {source} {key}");
            var contains = SqliteJsonFunctions.ArrayContains(source, key, "x");
            var expected = row[5] is bool b ? (b ? 1L : 0L) : (long?)null;
            if (row[6] is bool isArray && !isArray) expected = 0L;
            if ((long?)contains != expected) problems.Add($"array has {source} {key}: postgres {row[5]}/{row[6]} emulated {contains}");
            if ((string?)SqliteJsonFunctions.ConcatDocuments(source, other) != (string)row[7]!) problems.Add($"|| {source} {other}\n  postgres {row[7]}\n  emulated {SqliteJsonFunctions.ConcatDocuments(source, other)}");
            if (row[8] is string removed && (string?)SqliteJsonFunctions.RemoveKey(source, key) != removed) problems.Add($"- {source} {key}\n  postgres {removed}\n  emulated {SqliteJsonFunctions.RemoveKey(source, key)}");
        }
        Assert.True(problems.Count == 0, rows.Count + " rows\n" + string.Join("\n", problems.Take(12)));
    }

    [SkippableFact(DisplayName = "building a one-member object from an integer, a real, text and NULL equals PostgreSQL's jsonb_build_object, and the timestamp-to-milliseconds rule floors the way the driver stores the instant")]
    public async Task Object_building_and_epoch_agree()
    {
        fixture.RequireAvailable();
        var rows = await fixture.RowsAsync(
            "SELECT jsonb_build_object('k'::text, 5)::text, jsonb_build_object('é\"k'::text, 'x'::text)::text, jsonb_build_object('k'::text, null::int)::text, "
            + "jsonb_build_object('k'::text, 1.5::float8)::text, jsonb_build_object('k'::text, 100000000000000000000::float8)::text, jsonb_build_object('k'::text, 0.00001::float8)::text, "
            + "jsonb_build_object('k'::text, 'Infinity'::float8)::text, jsonb_build_object('k'::text, -9223372036854775807::bigint)::text, "
            + "floor(extract(epoch from timestamptz '2026-01-02 03:04:05.123456+00') * 1000)::double precision, floor(extract(epoch from timestamptz '1969-12-31 23:59:59.5+00') * 1000)::double precision");
        var row = rows[0];

        Assert.Equal((string)row[0]!, (string)SqliteJsonFunctions.BuildObject("k", 5L)!);
        Assert.Equal((string)row[1]!, (string)SqliteJsonFunctions.BuildObject("é\"k", "x")!);
        Assert.Equal((string)row[2]!, (string)SqliteJsonFunctions.BuildObject("k", null)!);
        Assert.Equal((string)row[3]!, (string)SqliteJsonFunctions.BuildObject("k", 1.5)!);
        Assert.Equal((string)row[4]!, (string)SqliteJsonFunctions.BuildObject("k", 1e20)!);
        Assert.Equal((string)row[5]!, (string)SqliteJsonFunctions.BuildObject("k", 0.00001)!);
        Assert.Equal((string)row[6]!, (string)SqliteJsonFunctions.BuildObject("k", double.PositiveInfinity)!);
        Assert.Equal((string)row[7]!, (string)SqliteJsonFunctions.BuildObject("k", -9223372036854775807L)!);
        Assert.Equal(Convert.ToDouble(row[8], CultureInfo.InvariantCulture), SqliteJsonFunctions.EpochMilliseconds("2026-01-02T03:04:05.1234560Z"));
        Assert.Equal(Convert.ToDouble(row[9], CultureInfo.InvariantCulture), SqliteJsonFunctions.EpochMilliseconds("1969-12-31T23:59:59.5000000Z"));
    }
}
