using Microsoft.Data.Sqlite;

namespace Neemle.Orm.Lite.Sqlite;

internal static class SqliteDataSourceName
{
    private const string Macro = "|DataDirectory|";

    private const string UriPrefix = "file:";

    private const string MemoryName = ":memory:";

    internal static (string? File, string? Memory) Opened(string connectionString, string? dataDirectory)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var source = builder.DataSource;
        var shared = builder.Cache == SqliteCacheMode.Shared;
        if (builder.Mode == SqliteOpenMode.Memory) return (null, shared ? source : null);
        if (string.IsNullOrEmpty(source) || source == MemoryName) return default;
        if (source.StartsWith(UriPrefix, StringComparison.Ordinal)) return FromUri(source[UriPrefix.Length..], shared);
        return (PlainFile(builder, dataDirectory), null);
    }

    internal static string? Pinned(SqliteConnectionStringBuilder builder, string? dataDirectory)
    {
        var source = builder.DataSource;
        if (builder.Mode == SqliteOpenMode.Memory || string.IsNullOrEmpty(source)) return null;
        if (source.StartsWith(UriPrefix, StringComparison.Ordinal)) return PinnedUri(source);
        return PlainFile(builder, dataDirectory);
    }

    internal static string? PlainFile(SqliteConnectionStringBuilder builder, string? dataDirectory)
    {
        var source = builder.DataSource;
        if (builder.Mode == SqliteOpenMode.Memory || string.IsNullOrEmpty(source) || source == MemoryName) return null;
        if (source.StartsWith(UriPrefix, StringComparison.Ordinal)) return null;
        return Path.GetFullPath(Expand(source, dataDirectory));
    }

    private static string Expand(string source, string? dataDirectory)
    {
        if (string.IsNullOrEmpty(dataDirectory)) return source;
        if (source.StartsWith(UriPrefix, StringComparison.OrdinalIgnoreCase)) return source;
        if (source.Equals(MemoryName, StringComparison.OrdinalIgnoreCase)) return source;
        if (source.StartsWith(Macro, StringComparison.InvariantCultureIgnoreCase))
            return Path.Combine(dataDirectory, source[Macro.Length..]);
        return Path.IsPathRooted(source) ? source : Path.Combine(dataDirectory, source);
    }

    private static string? PinnedUri(string source)
    {
        var rest = source[UriPrefix.Length..];
        var (location, query) = SplitUri(rest);
        if (location.StartsWith("//", StringComparison.Ordinal) || UriPath(location) is not { } path) return null;
        if (path.Length == 0 || Path.IsPathRooted(path) || IsMemory(path, query)) return null;
        var full = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows() && full.StartsWith(@"\\", StringComparison.Ordinal)) return null;
        return UriPrefix + EscapedPath(full) + rest[location.Length..];
    }

    private static string EscapedPath(string full)
    {
        var text = full.Replace("%", "%25").Replace("?", "%3F").Replace("#", "%23");
        return OperatingSystem.IsWindows() ? "/" + text.Replace('\\', '/') : text;
    }

    private static (string? File, string? Memory) FromUri(string rest, bool shared)
    {
        var (location, query) = SplitUri(rest);
        if (UriPath(location) is not { } path) return default;
        if (IsMemory(path, query)) return (null, shared || Parameter(query, "cache") == "shared" ? path : null);
        if (path.Length == 0) return default;
        return (Path.GetFullPath(path), null);
    }

    private static bool IsMemory(string path, string query) =>
        path == MemoryName || Parameter(query, "mode") == "memory";

    private static (string Location, string Query) SplitUri(string rest)
    {
        var hash = rest.IndexOf('#');
        var body = hash < 0 ? rest : rest[..hash];
        var split = body.IndexOf('?');
        return split < 0 ? (body, string.Empty) : (body[..split], body[(split + 1)..]);
    }

    private static string? UriPath(string location)
    {
        var path = location;
        if (location.StartsWith("//", StringComparison.Ordinal))
        {
            var slash = location.IndexOf('/', 2);
            var authority = slash < 0 ? location[2..] : location[2..slash];
            if (authority.Length != 0 && authority != "localhost") return null;
            path = slash < 0 ? string.Empty : location[slash..];
        }
        return DropDriveSlash(Decode(path));
    }

    private static string Decode(string escaped)
    {
        var text = Uri.UnescapeDataString(escaped);
        var nul = text.IndexOf('\0');
        return nul < 0 ? text : text[..nul];
    }

    private static string DropDriveSlash(string path)
    {
        var drive = path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':';
        return OperatingSystem.IsWindows() && drive ? path[1..] : path;
    }

    private static string? Parameter(string query, string name)
    {
        string? found = null;
        foreach (var pair in query.Split('&'))
        {
            var equals = pair.IndexOf('=');
            if (equals > 0 && Decode(pair[..equals]) == name) found = Decode(pair[(equals + 1)..]);
        }
        return found;
    }
}
