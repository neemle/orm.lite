using System.Globalization;
using System.Text.Json;

namespace Neemle.Orm.Lite;

public sealed partial class OrmLiteOptions
{
    public const string DefaultSection = "OrmLite";

    public const string DefaultSettingsFile = "appsettings.json";

    private static readonly string[] KnownSettings =
    [
        nameof(Provider), nameof(ConnectionString), nameof(Host), nameof(Port), nameof(Database),
        nameof(Username), nameof(Password), nameof(Path), nameof(Pooling), nameof(BusyTimeoutSeconds),
    ];

    private const string DiagnosticsPrefix = "Diagnostics:";

    public static OrmLiteOptions FromConnectionString(string provider, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return new OrmLiteOptions { Provider = provider, ConnectionString = connectionString };
    }

    public static OrmLiteOptions FromSettings(IEnumerable<KeyValuePair<string, string?>> settings, string section = DefaultSection)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        var options = new OrmLiteOptions();
        var prefix = section + ":";
        foreach (var (rawKey, value) in settings)
        {
            var key = rawKey.Replace("__", ":", StringComparison.Ordinal);
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (value is null) continue;
            options.Apply(key[prefix.Length..], value, rawKey);
        }
        return options;
    }

    public static OrmLiteOptions FromEnvironment(string section = DefaultSection)
    {
        var settings = new List<KeyValuePair<string, string?>>();
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            settings.Add(new((string)entry.Key, entry.Value as string));
        return FromSettings(settings, section);
    }

    public static OrmLiteOptions FromJsonFile(string path, string section = DefaultSection, bool optional = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        if (!File.Exists(path))
        {
            if (optional) return new OrmLiteOptions();
            throw new FileNotFoundException($"the settings file '{path}' does not exist.", path);
        }
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var settings = new List<KeyValuePair<string, string?>>();
        foreach (var found in SectionsOf(document.RootElement, section.Split(':'), 0))
            Flatten(section, found, settings);
        return FromSettings(settings, section);
    }

    private static bool IsSegment(string name, string segment) =>
        string.Equals(name, segment, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<JsonElement> SectionsOf(JsonElement element, string[] path, int depth)
    {
        if (element.ValueKind != JsonValueKind.Object) yield break;
        var rest = string.Join(':', path[depth..]);
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, rest, StringComparison.OrdinalIgnoreCase))
                yield return property.Value;
            else if (depth < path.Length - 1 && IsSegment(property.Name, path[depth]))
                foreach (var inner in SectionsOf(property.Value, path, depth + 1)) yield return inner;
        }
    }

    public static OrmLiteOptions Load(string section = DefaultSection)
    {
        var file = System.IO.Path.Combine(AppContext.BaseDirectory, DefaultSettingsFile);
        if (!File.Exists(file)) file = DefaultSettingsFile;
        var options = FromJsonFile(file, section, optional: true);
        options.Overlay(FromEnvironment(section));
        return options;
    }

    private static void Flatten(string key, JsonElement element, List<KeyValuePair<string, string?>> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) Flatten(key + ":" + property.Name, property.Value, into);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray()) Flatten(key + ":" + index++.ToString(CultureInfo.InvariantCulture), item, into);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            default:
                into.Add(new(key, element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText()));
                break;
        }
    }

    private void Apply(string name, string value, string source)
    {
        switch (KnownSettings.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase)))
        {
            case nameof(Provider): Provider = value; break;
            case nameof(ConnectionString): ConnectionString = value; break;
            case nameof(Host): Host = value; break;
            case nameof(Port): Port = Integer(value, source); break;
            case nameof(Database): Database = value; break;
            case nameof(Username): Username = value; break;
            case nameof(Password): Password = value; break;
            case nameof(Path): Path = value; break;
            case nameof(Pooling): Pooling = Boolean(value, source); break;
            case nameof(BusyTimeoutSeconds): BusyTimeoutSeconds = Integer(value, source); break;
            default:
                if (name.StartsWith(DiagnosticsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    Diagnostics.Apply(name[DiagnosticsPrefix.Length..], value, source);
                    break;
                }
                throw new ArgumentException(
                    $"'{source}' is not an Orm.Lite setting; the settings are {string.Join(", ", KnownSettings)}, and {DiagnosticsPrefix}{{{string.Join(", ", OrmLiteDiagnosticsOptions.SettingNames)}}}.");
        }
    }

    private void Overlay(OrmLiteOptions other)
    {
        Provider = other.Provider ?? Provider;
        ConnectionString = other.ConnectionString ?? ConnectionString;
        Host = other.Host ?? Host;
        Port = other.Port ?? Port;
        Database = other.Database ?? Database;
        Username = other.Username ?? Username;
        Password = other.Password ?? Password;
        Path = other.Path ?? Path;
        Pooling = other.Pooling ?? Pooling;
        BusyTimeoutSeconds = other.BusyTimeoutSeconds ?? BusyTimeoutSeconds;
        Diagnostics.Overlay(other.Diagnostics);
    }

    internal static int Integer(string value, string source) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"the Orm.Lite setting '{source}' is '{value}', which is not a whole number.");

    internal static bool Boolean(string value, string source) =>
        bool.TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException($"the Orm.Lite setting '{source}' is '{value}', which is not true or false.");
}
