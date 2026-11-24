using System.Globalization;

namespace Neemle.OrmLite.Sqlite;

public static class SqliteValueCodec
{
    public const string TimestampFormat = SqliteDataLiterals.TimestampFormat;

    public static object? Encode(object? value) => value switch
    {
        Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
        DateTime moment => Timestamp(moment),
        DateTimeOffset moment => Timestamp(moment.UtcDateTime),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        _ => value,
    };

    public static string Timestamp(DateTime moment)
    {
        var utc = moment.Kind == DateTimeKind.Local ? moment.ToUniversalTime() : moment;
        return utc.ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }
}
