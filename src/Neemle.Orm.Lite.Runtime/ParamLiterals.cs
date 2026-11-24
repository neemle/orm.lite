using System.Collections;
using System.Globalization;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public static class ParamLiterals
{
    // The one spelling of a moment, shared with the SQLite value codec (Neemle.Orm.Lite.Sqlite reads it from here).
    public const string MomentFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public static object? Normalise(object? value) => value switch
    {
        Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
        DateTime moment => (moment.Kind == DateTimeKind.Local ? moment.ToUniversalTime() : moment)
            .ToString(MomentFormat, CultureInfo.InvariantCulture),
        DateTimeOffset moment => moment.UtcDateTime.ToString(MomentFormat, CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Jsonb document => document.Json,
        Enum member => member.ToString(),
        _ => value,
    };

    public static Node Of(object? value) => Normalise(value) switch
    {
        IEnumerable and not string => throw new NotSupportedException(
            "a view definition is stored in the database, so it holds scalar values only; a collection parameter cannot be written into it."),
        var scalar => DdlLiterals.Of(scalar),
    };
}
