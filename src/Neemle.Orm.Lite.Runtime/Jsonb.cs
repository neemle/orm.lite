#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Neemle.Orm.Lite;

    public sealed class Jsonb : IEquatable<Jsonb>
    {
        public const string ColumnType = "jsonb";

        public const string EmptyDocumentDefaultSql = "'{}'::jsonb";

        private readonly string _json;

        private Jsonb(string json) => _json = json;

        public string Json => _json;

        public static Jsonb Parse(string json)
        {
            if (json is null) throw new ArgumentNullException(nameof(json));
            using (JsonDocument.Parse(json)) { }
            return new Jsonb(json);
        }

        public static bool TryParse(string? json, out Jsonb? value)
        {
            value = null;
            if (json is null) return false;
            try { using (JsonDocument.Parse(json)) { } }
            catch (JsonException) { return false; }
            value = new Jsonb(json);
            return true;
        }

        public static Jsonb FromStorage(string json) =>
            new Jsonb(json ?? throw new ArgumentNullException(nameof(json)));

        public override string ToString() => _json;

        public bool Equals(Jsonb? other) =>
            other is not null && string.Equals(_json, other._json, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as Jsonb);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_json);

        public static bool SameDocument(Jsonb? left, Jsonb? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            using var a = JsonDocument.Parse(left._json);
            using var b = JsonDocument.Parse(right._json);
            return SameElement(a.RootElement, b.RootElement);
        }

        private static bool SameElement(JsonElement a, JsonElement b)
        {
            if (a.ValueKind != b.ValueKind) return false;
            switch (a.ValueKind)
            {
                case JsonValueKind.Object:
                    var left = LastValuePerKey(a);
                    var right = LastValuePerKey(b);
                    if (left.Count != right.Count) return false;
                    foreach (var pair in left)
                    {
                        if (!right.TryGetValue(pair.Key, out var other)) return false;
                        if (!SameElement(pair.Value, other)) return false;
                    }
                    return true;
                case JsonValueKind.Array:
                    var ea = a.EnumerateArray();
                    var eb = b.EnumerateArray();
                    while (true)
                    {
                        var moreA = ea.MoveNext();
                        var moreB = eb.MoveNext();
                        if (moreA != moreB) return false;
                        if (!moreA) return true;
                        if (!SameElement(ea.Current, eb.Current)) return false;
                    }
                case JsonValueKind.String:
                    return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
                case JsonValueKind.Number:
                    return SameNumber(a.GetRawText(), b.GetRawText());
                default:
                    return true;
            }
        }

        private static Dictionary<string, JsonElement> LastValuePerKey(JsonElement element)
        {
            var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) map[property.Name] = property.Value;
            return map;
        }

        private static bool SameNumber(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal)) return true;
            if (!TryCanonicalise(a, out var leftNegative, out var leftDigits, out var leftExponent)) return false;
            if (!TryCanonicalise(b, out var rightNegative, out var rightDigits, out var rightExponent)) return false;
            return leftNegative == rightNegative
                && leftExponent == rightExponent
                && string.Equals(leftDigits, rightDigits, StringComparison.Ordinal);
        }

        private static bool TryCanonicalise(string raw, out bool negative, out string digits, out long exponent)
        {
            negative = false;
            digits = "0";
            exponent = 0;

            var i = 0;
            if (i < raw.Length && (raw[i] == '-' || raw[i] == '+')) { negative = raw[i] == '-'; i++; }

            var integerStart = i;
            while (i < raw.Length && char.IsAsciiDigit(raw[i])) i++;
            var integerPart = raw[integerStart..i];

            var fractionPart = "";
            if (i < raw.Length && raw[i] == '.')
            {
                i++;
                var fractionStart = i;
                while (i < raw.Length && char.IsAsciiDigit(raw[i])) i++;
                fractionPart = raw[fractionStart..i];
            }

            long written = 0;
            if (i < raw.Length && (raw[i] == 'e' || raw[i] == 'E'))
            {
                i++;
                var exponentStart = i;
                if (i < raw.Length && (raw[i] == '+' || raw[i] == '-')) i++;
                while (i < raw.Length && char.IsAsciiDigit(raw[i])) i++;
                if (!long.TryParse(
                        raw[exponentStart..i],
                        System.Globalization.NumberStyles.AllowLeadingSign,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out written))
                    return false;
            }

            if (i != raw.Length || integerPart.Length + fractionPart.Length == 0) return false;

            var all = integerPart + fractionPart;
            var scale = (decimal)written - fractionPart.Length;

            var first = 0;
            while (first < all.Length && all[first] == '0') first++;
            var last = all.Length;
            while (last > first && all[last - 1] == '0') { last--; scale += 1; }

            if (first >= last) { negative = false; digits = "0"; exponent = 0; return true; }
            if (scale < long.MinValue || scale > long.MaxValue) return false;

            digits = all[first..last];
            exponent = (long)scale;
            return true;
        }
    }
