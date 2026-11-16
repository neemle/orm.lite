#nullable enable
using System;
using System.Data.Common;

namespace Neemle.Orm.Lite
{
    public static class OrmEnum
    {
        public static TEnum Read<TEnum>(DbDataReader r, int ordinal, string table, string column, string keyColumn)
            where TEnum : struct, Enum
        {
            var raw = r.GetString(ordinal);
            if (TryParseExact<TEnum>(raw, out var value)) return value;
            throw new OrmEnumValueException(table, column, RowKey(r, keyColumn), raw, Enum.GetNames<TEnum>());
        }

        public static bool TryParseExact<TEnum>(string? stored, out TEnum value) where TEnum : struct, Enum
        {
            value = default;
            if (string.IsNullOrWhiteSpace(stored)) return false;
            var trimmed = stored.Trim();
            return Enum.TryParse(trimmed, ignoreCase: true, out value)
                   && Enum.IsDefined(value)
                   && string.Equals(value.ToString(), trimmed, StringComparison.OrdinalIgnoreCase);
        }

        private static string RowKey(DbDataReader r, string keyColumn)
        {
            if (keyColumn.Length == 0) return "";
            try
            {
                var o = r.GetOrdinal(keyColumn);
                return r.IsDBNull(o) ? "" : (r.GetValue(o)?.ToString() ?? "");
            }
#pragma warning disable CA1031
            catch { return ""; }
#pragma warning restore CA1031
        }
    }

    public sealed class OrmEnumValueException : InvalidOperationException
    {
        public OrmEnumValueException(string table, string column, string rowKey, string value, string[] accepted)
            : base(Describe(table, column, rowKey, value, accepted))
        {
            Table = table;
            Column = column;
            RowKey = rowKey;
            Value = value;
            Accepted = accepted;
        }

        public string Table { get; }
        public string Column { get; }
        public string RowKey { get; }

        public string Value { get; }

        public string[] Accepted { get; }

        private static string Describe(string table, string column, string rowKey, string value, string[] accepted) =>
            table + "." + column + (rowKey.Length > 0 ? " on row " + rowKey : "")
            + " holds \"" + value + "\", which is not one of: " + string.Join(", ", accepted)
            + ". The value is reported rather than replaced with a default, because answering with a "
            + "priority the row does not hold would hide the bad data instead of surfacing it. Repair the "
            + "row (or add the member) — this is a data fault, not a request fault.";
    }
}
