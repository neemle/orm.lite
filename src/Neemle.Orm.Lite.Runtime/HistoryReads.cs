#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace Neemle.Orm.Lite;

internal static class HistoryReads
{
    internal static bool Flag(object? value) =>
        value is not null and not DBNull && Convert.ToBoolean(value, CultureInfo.InvariantCulture);

    internal static string[] Keys(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        if (raw is string[] keys) return keys;
        if (raw is not string text)
            throw new InvalidOperationException("the changed keys of a history row came back as " + raw.GetType().Name + ", not a list.");
        using var document = JsonDocument.Parse(text);
        var found = new List<string>();
        foreach (var item in document.RootElement.EnumerateArray()) found.Add(item.GetString()!);
        return [.. found];
    }

    internal static object OwnerValue(object value, bool identifier) =>
        identifier && value is string text && Guid.TryParse(text, out var guid) ? guid : value;

    internal static DateTime Instant(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            DateTime moment => DateTime.SpecifyKind(moment, DateTimeKind.Utc),
            string text => DateTime.Parse(
                text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => throw new InvalidOperationException("a history timestamp came back as " + raw.GetType().Name + "."),
        };
    }
}
