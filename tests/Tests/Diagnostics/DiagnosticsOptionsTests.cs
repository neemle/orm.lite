using Xunit;

namespace Neemle.OrmLite.Tests.Diagnostics;

[Collection(ProcessStateCollection.Name)]
public sealed class DiagnosticsOptionsTests
{
    private static KeyValuePair<string, string?> S(string key, string? value) => new(key, value);

    [Fact(DisplayName = "every diagnostics setting binds from a colon key or a double-underscore key, whatever the case")]
    public void Every_setting_binds()
    {
        var options = OrmLiteOptions.FromSettings(
        [
            S("OrmLite:Diagnostics:StatementLevel", "debug"),
            S("OrmLite__Diagnostics__SlowThresholdMilliseconds", "250"),
            S("OrmLite:Diagnostics:SlowLevel", "Error"),
            S("OrmLite:diagnostics:TransactionLevel", "Information"),
            S("OrmLite:Diagnostics:SampleRate", "0.25"),
            S("OrmLite:Diagnostics:LogParameters", "true"),
            S("OrmLite:Diagnostics:ParameterMaxLength", "12"),
        ]);

        var diagnostics = options.Diagnostics;
        Assert.Equal(OrmLogLevel.Debug, diagnostics.StatementLevel);
        Assert.Equal(TimeSpan.FromMilliseconds(250), diagnostics.SlowThreshold);
        Assert.Equal(OrmLogLevel.Error, diagnostics.SlowLevel);
        Assert.Equal(OrmLogLevel.Information, diagnostics.TransactionLevel);
        Assert.Equal(0.25, diagnostics.SampleRate);
        Assert.True(diagnostics.LogParameters);
        Assert.Equal(12, diagnostics.ParameterMaxLength);
    }

    [Fact(DisplayName = "the defaults report nothing, keep parameter values out and sample everything")]
    public void Defaults_are_quiet()
    {
        var diagnostics = new OrmLiteOptions().Diagnostics;

        Assert.Equal(OrmLogLevel.None, diagnostics.StatementLevel);
        Assert.Equal(TimeSpan.Zero, diagnostics.SlowThreshold);
        Assert.Equal(OrmLogLevel.Warning, diagnostics.SlowLevel);
        Assert.Equal(OrmLogLevel.None, diagnostics.TransactionLevel);
        Assert.Equal(1, diagnostics.SampleRate);
        Assert.False(diagnostics.LogParameters);
        Assert.Equal(64, diagnostics.ParameterMaxLength);
        Assert.False(diagnostics.Reports);
    }

    [Fact(DisplayName = "the diagnostics settings load from a JSON block and an environment variable overrides one of them")]
    public void Json_block_and_environment_overlay()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("ormlite-diag-").FullName, "settings.json");
        File.WriteAllText(path, """{ "OrmLiteDiag": { "Provider": "sqlite", "Diagnostics": { "SlowThresholdMilliseconds": 200, "StatementLevel": "Trace" } } }""");
        var variable = "OrmLiteDiag__Diagnostics__StatementLevel";
        Environment.SetEnvironmentVariable(variable, "Warning");
        try
        {
            var options = OrmLiteOptions.FromJsonFile(path, "OrmLiteDiag");
            Assert.Equal(OrmLogLevel.Trace, options.Diagnostics.StatementLevel);
            Assert.Equal(200, options.Diagnostics.SlowThresholdMilliseconds);

            var overlay = OrmLiteOptions.FromEnvironment("OrmLiteDiag");
            Assert.Equal(OrmLogLevel.Warning, overlay.Diagnostics.StatementLevel);
            Assert.Equal(0, overlay.Diagnostics.SlowThresholdMilliseconds);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory(DisplayName = "a diagnostics setting that is not one, or whose value does not parse, is a start-up error that names it")]
    [InlineData("OrmLite:Diagnostics:Nope", "1", typeof(ArgumentException), "OrmLite:Diagnostics:Nope")]
    [InlineData("OrmLite:Diagnostics:StatementLevel", "loud", typeof(FormatException), "loud")]
    [InlineData("OrmLite:Diagnostics:SampleRate", "most", typeof(FormatException), "most")]
    [InlineData("OrmLite:Diagnostics:LogParameters", "maybe", typeof(FormatException), "maybe")]
    [InlineData("OrmLite:Diagnostics:SlowThresholdMilliseconds", "1.5", typeof(FormatException), "1.5")]
    public void Bad_settings_are_refused(string key, string value, Type error, string mention)
    {
        var thrown = Assert.Throws(error, () => OrmLiteOptions.FromSettings([S(key, value)]));
        Assert.Contains(mention, thrown.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a level or threshold with no sink, and a rate or length out of range, are refused when the context opens")]
    public async Task Misconfiguration_is_refused_at_open()
    {
        await Refused(o => o.StatementLevel = OrmLogLevel.Debug, "no Sink");
        await Refused(o => o.SlowThresholdMilliseconds = 5, "no Sink");
        await Refused(o => o.TransactionLevel = OrmLogLevel.Debug, "no Sink");
        await Refused(o => { o.Sink = new Quiet(); o.SampleRate = 0; }, "SampleRate");
        await Refused(o => { o.Sink = new Quiet(); o.SampleRate = 1.5; }, "SampleRate");
        await Refused(o => { o.Sink = new Quiet(); o.ParameterMaxLength = 0; }, "ParameterMaxLength");
        await Refused(o => { o.Sink = new Quiet(); o.SlowThresholdMilliseconds = -1; }, "SlowThresholdMilliseconds");
    }

    private static async Task Refused(Action<OrmLiteDiagnosticsOptions> configure, string mention)
    {
        Sqlite.SqliteProviderFactory.Register();
        var options = new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(Path.GetTempPath(), "never-opened.db") };
        configure(options.Diagnostics);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => OrmLite.OpenAsync(options));
        Assert.Contains(mention, thrown.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "text that looks like a secret is redacted and ordinary text is not")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln", true)]
    [InlineData("Bearer abc", true)]
    [InlineData("basic dXNlcjpwYXNz", true)]
    [InlineData("-----BEGIN PRIVATE KEY-----", true)]
    [InlineData("$2b$12$abcdefghijklmnopqrstuv", true)]
    [InlineData("$argon2id$v=19$m=65536", true)]
    [InlineData("postgres://user:pw@host/db", true)]
    [InlineData("Host=h;Password=x", true)]
    [InlineData("api_key=abc", true)]
    [InlineData("3f2a9c1de4b84a7e9f0a1b2c3d4e5f60", true)]
    [InlineData("hello world", false)]
    [InlineData("Quarterly report", false)]
    [InlineData("https://example.com/a?b=c", false)]
    [InlineData("a-slug-made-only-of-letters-and-dashes", false)]
    [InlineData("short1", false)]
    public void Secret_shape_is_recognised(string text, bool secret) =>
        Assert.Equal(secret, ParameterRedactor.IsSecretShaped(text));

    [Fact(DisplayName = "a described value is null for null, a length for bytes, invariant text for numbers and cut when long")]
    public void Values_are_described()
    {
        Assert.Null(ParameterRedactor.Describe(null, 10));
        Assert.Null(ParameterRedactor.Describe(DBNull.Value, 10));
        Assert.Equal("[3 bytes]", ParameterRedactor.Describe(new byte[] { 1, 2, 3 }, 10));
        Assert.Equal("1234.5", ParameterRedactor.Describe(1234.5m, 10));
        Assert.Equal("true", ParameterRedactor.Describe(true, 10)?.ToLowerInvariant());
        Assert.Equal("abcdefghij...", ParameterRedactor.Describe("abcdefghijklmnop", 10));
        Assert.Equal(ParameterRedactor.Redacted, ParameterRedactor.Describe("token=abc", 10));
    }

    private sealed class Quiet : IOrmDiagnosticsSink
    {
        public void OnStatement(StatementEvent statement)
        {
        }

        public void OnTransaction(TransactionEvent transaction)
        {
        }
    }
}
