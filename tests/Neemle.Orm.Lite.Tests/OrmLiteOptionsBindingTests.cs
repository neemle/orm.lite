using Xunit;

namespace Neemle.Orm.Lite.Tests;

[Collection(ProcessStateCollection.Name)]
public sealed class OrmLiteOptionsBindingTests : IDisposable
{
    private const string Section = "OrmLiteBindingTest";

    private readonly string _folder = Directory.CreateTempSubdirectory("ormlite-options-").FullName;

    private readonly List<string> _variables = [];

    private readonly string _baseFile = Path.Combine(AppContext.BaseDirectory, OrmLiteOptions.DefaultSettingsFile);

    public void Dispose()
    {
        foreach (var name in _variables) Environment.SetEnvironmentVariable(name, null);
        if (File.Exists(_baseFile)) File.Delete(_baseFile);
        Directory.Delete(_folder, true);
    }

    private void Env(string name, string value)
    {
        _variables.Add(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    private string JsonFile(string text)
    {
        var path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, text);
        return path;
    }

    private static KeyValuePair<string, string?> S(string key, string? value) => new(key, value);

    [Fact(DisplayName = "FromSettings binds every setting, whatever the key case and whether the separator is a colon or a double underscore")]
    public void Every_setting_binds()
    {
        var options = OrmLiteOptions.FromSettings(
        [
            S("OrmLite:Provider", "postgres"),
            S("ormlite:connectionstring", "Host=h"),
            S("OrmLite__Host", "db.example"),
            S("OrmLite:Port", "5433"),
            S("OrmLite:Database", "d"),
            S("OrmLite:USERNAME", "u"),
            S("OrmLite__Password", "p"),
            S("OrmLite:Path", "x.db"),
            S("OrmLite:Pooling", "false"),
            S("OrmLite:BusyTimeoutSeconds", "7"),
        ]);

        Assert.Equal("postgres", options.Provider);
        Assert.Equal("Host=h", options.ConnectionString);
        Assert.Equal("db.example", options.Host);
        Assert.Equal(5433, options.Port);
        Assert.Equal("d", options.Database);
        Assert.Equal("u", options.Username);
        Assert.Equal("p", options.Password);
        Assert.Equal("x.db", options.Path);
        Assert.False(options.Pooling);
        Assert.Equal(7, options.BusyTimeoutSeconds);
    }

    [Fact(DisplayName = "FromSettings leaves other sections alone and the later value of a repeated key wins")]
    public void Other_sections_are_ignored_and_the_last_value_wins()
    {
        var options = OrmLiteOptions.FromSettings(
        [
            S("Logging:LogLevel:Default", "Debug"),
            S("OrmLiteExtra:Provider", "postgres"),
            S("OrmLite:Provider", "sqlite"),
            S("OrmLite__Provider", "postgres"),
            S("OrmLite:Host", null),
        ]);

        Assert.Equal("postgres", options.Provider);
        Assert.Null(options.Host);
    }

    [Fact(DisplayName = "FromSettings refuses a key of the section that is not a setting and names it with the valid ones")]
    public void An_unknown_key_is_refused()
    {
        var refused = Assert.Throws<ArgumentException>(() => OrmLiteOptions.FromSettings([S("OrmLite:BusyTimeout", "5")]));

        Assert.Contains("'OrmLite:BusyTimeout'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("BusyTimeoutSeconds", refused.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "FromSettings refuses a port or a boolean that is not one, naming the key and the value")]
    public void A_malformed_value_is_refused()
    {
        var port = Assert.Throws<FormatException>(() => OrmLiteOptions.FromSettings([S("OrmLite:Port", "five")]));
        var flag = Assert.Throws<FormatException>(() => OrmLiteOptions.FromSettings([S("OrmLite__Pooling", "maybe")]));

        Assert.Contains("'OrmLite:Port' is 'five'", port.Message, StringComparison.Ordinal);
        Assert.Contains("'OrmLite__Pooling' is 'maybe'", flag.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "FromSettings reads a named section")]
    public void A_named_section_is_read()
    {
        var options = OrmLiteOptions.FromSettings([S("Data:Provider", "sqlite"), S("OrmLite:Provider", "postgres")], "Data");

        Assert.Equal("sqlite", options.Provider);
    }

    [Fact(DisplayName = "FromConnectionString names the provider and its connection string")]
    public void A_connection_string_binds()
    {
        var options = OrmLiteOptions.FromConnectionString("postgres", "Host=h;Database=d");

        Assert.Equal("postgres", options.Provider);
        Assert.Equal("Host=h;Database=d", options.ConnectionString);
        Assert.Throws<ArgumentException>(() => OrmLiteOptions.FromConnectionString("", "x"));
        Assert.Throws<ArgumentException>(() => OrmLiteOptions.FromConnectionString("sqlite", " "));
    }

    [Fact(DisplayName = "FromEnvironment reads Section__Setting variables")]
    public void The_environment_binds()
    {
        Env(Section + "__Provider", "sqlite");
        Env(Section + "__Path", "env.db");
        Env(Section + "__Port", "1234");

        var options = OrmLiteOptions.FromEnvironment(Section);

        Assert.Equal("sqlite", options.Provider);
        Assert.Equal("env.db", options.Path);
        Assert.Equal(1234, options.Port);
        Assert.Null(options.Host);
    }

    [Fact(DisplayName = "FromJsonFile reads the section of a file with comments, numbers and booleans, and ignores the rest of the file")]
    public void A_json_file_binds()
    {
        var path = JsonFile("{ // a comment\n \"Logging\": { \"Level\": \"x\" },\n \"" + Section.ToLowerInvariant() + "\": { \"Provider\": \"postgres\", \"Port\": 5432, \"Pooling\": true, \"Host\": \"h\", \"Password\": null, },\n}");

        var options = OrmLiteOptions.FromJsonFile(path, Section);

        Assert.Equal("postgres", options.Provider);
        Assert.Equal(5432, options.Port);
        Assert.True(options.Pooling);
        Assert.Equal("h", options.Host);
        Assert.Null(options.Password);
    }

    private OrmLiteOptions From(string json) => OrmLiteOptions.FromJsonFile(JsonFile(json), "App:OrmLite");

    [Fact(DisplayName = "FromJsonFile reads a nested section path such as App:OrmLite, whatever the key case, "
        + "and still a flat property named like the whole path")]
    public void A_nested_section_path_binds()
    {
        var nested = OrmLiteOptions.FromJsonFile(
            JsonFile("{ \"app\": { \"ORMLITE\": { \"Provider\": \"sqlite\", \"Path\": \"x.db\" } } }"), "App:OrmLite");
        var flat = From("{ \"App:OrmLite\": { \"Provider\": \"postgres\" } }");
        var absent = From("{ \"App\": { \"Other\": { \"Provider\": \"x\" } } }");
        var scalar = From("{ \"App\": 3 }");

        Assert.Equal("sqlite", nested.Provider);
        Assert.Equal("x.db", nested.Path);
        Assert.Equal("postgres", flat.Provider);
        Assert.Null(absent.Provider);
        Assert.Null(scalar.Provider);
    }

    [Fact(DisplayName = "FromJsonFile refuses a missing file unless it is optional, and a nested key it does not know")]
    public void A_json_file_is_checked()
    {
        var missing = Path.Combine(_folder, "none.json");

        Assert.Throws<FileNotFoundException>(() => OrmLiteOptions.FromJsonFile(missing));
        Assert.Null(OrmLiteOptions.FromJsonFile(missing, optional: true).Provider);
        Assert.Null(OrmLiteOptions.FromJsonFile(JsonFile("{ \"Other\": {} }"), Section).Provider);
        var nested = Assert.Throws<ArgumentException>(() => OrmLiteOptions.FromJsonFile(JsonFile("{ \"" + Section + "\": { \"Extra\": { \"A\": 1 } } }"), Section));
        Assert.Contains("Extra:A", nested.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Load takes appsettings.json and lets the environment override it setting by setting")]
    public void Load_overlays_the_environment_on_the_file()
    {
        File.WriteAllText(_baseFile, "{ \"" + Section + "\": { \"Provider\": \"sqlite\", \"Path\": \"file.db\", \"BusyTimeoutSeconds\": 3 } }");
        Env(Section + "__Path", "env.db");

        var options = OrmLiteOptions.Load(Section);

        Assert.Equal("sqlite", options.Provider);
        Assert.Equal("env.db", options.Path);
        Assert.Equal(3, options.BusyTimeoutSeconds);
    }

    [Fact(DisplayName = "Load with no file and no variables returns empty options for the caller to complete")]
    public void Load_with_nothing_is_empty()
    {
        var options = OrmLiteOptions.Load(Section);

        Assert.Null(options.Provider);
        Assert.Null(options.Path);
    }
}
