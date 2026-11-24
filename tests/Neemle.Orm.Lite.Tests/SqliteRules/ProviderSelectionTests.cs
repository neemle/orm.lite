using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Npgsql;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class ProviderSelectionTests
{
    public ProviderSelectionTests()
    {
        SqliteProviderFactory.Register();
        PostgresProviderFactory.Register();
    }

    [Fact(DisplayName = "a missing provider is a startup error that says there is no default and names the installed providers")]
    public async Task A_missing_provider_is_refused()
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => OrmLite.OpenAsync(new OrmLiteOptions { Path = "x.db" }));

        Assert.Contains("no provider is configured", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("no default provider", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("postgres, sqlite", refusal.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => OrmLite.OpenAsync(new OrmLiteOptions { Provider = "  " }));
    }

    [Fact(DisplayName = "an unknown provider names the package to add and lists the installed ones")]
    public async Task An_unknown_provider_names_the_package()
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => OrmLite.OpenAsync(new OrmLiteOptions { Provider = "oracle" }));

        Assert.Contains("provider 'oracle' is not installed; add the package Neemle.Orm.Lite.Oracle", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("to the application project (referencing the package registers the provider)", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("postgres, sqlite", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the provider id is matched without regard to case and surrounding spaces")]
    public void The_provider_id_is_forgiving()
    {
        Assert.Same(SqliteProviderFactory.Instance, ProviderRegistry.Resolve(new OrmLiteOptions { Provider = " SQLite " }));
        Assert.Same(PostgresProviderFactory.Instance, ProviderRegistry.Resolve(new OrmLiteOptions { Provider = "postgres" }));
    }

    [Fact(DisplayName = "registering the same provider again is harmless and a different provider under a taken id is refused")]
    public void Registration_is_idempotent_and_exclusive()
    {
        SqliteProviderFactory.Register();

        var refusal = Assert.Throws<InvalidOperationException>(() => ProviderRegistry.Register(new ImpostorFactory()));

        Assert.Contains("already registered", refusal.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => ProviderRegistry.Register((IProviderFactory)null!));
    }

    [Fact(DisplayName = "one registry serves both the factory lookup and the provider lookup")]
    public void One_registry_serves_both_lookups()
    {
        Assert.Equal("sqlite", ProviderRegistry.Require("sqlite").Id);
        Assert.Equal("postgres", ProviderRegistry.Require("postgres").Id);
        Assert.Contains("sqlite", ProviderRegistry.Installed);
    }

    [Theory(DisplayName = "an option that does not apply to the sqlite provider is refused by name")]
    [InlineData("Host")]
    [InlineData("Port")]
    [InlineData("Database")]
    [InlineData("Username")]
    [InlineData("Password")]
    public void Foreign_options_are_refused_for_sqlite(string option)
    {
        var options = new OrmLiteOptions { Provider = "sqlite", Path = "x.db" };
        switch (option)
        {
            case "Host": options.Host = "h"; break;
            case "Port": options.Port = 1; break;
            case "Database": options.Database = "d"; break;
            case "Username": options.Username = "u"; break;
            case "Password": options.Password = "p"; break;
        }

        var refusal = Assert.Throws<InvalidOperationException>(() => SqliteProviderFactory.ConnectionStringOf(options));

        Assert.Contains($"OrmLite:{option}", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the sqlite provider needs a path or a connection string, and refuses a timeout that would wait forever")]
    public void Sqlite_settings_are_validated()
    {
        Assert.Contains("OrmLite:Path", Assert.Throws<InvalidOperationException>(() => SqliteProviderFactory.ConnectionStringOf(new OrmLiteOptions())).Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => SqliteProviderFactory.ConnectionStringOf(new OrmLiteOptions { Path = "x.db", BusyTimeoutSeconds = 0 }));
        Assert.Throws<InvalidOperationException>(() => SqliteProviderFactory.ConnectionStringOf(new OrmLiteOptions { Path = "x.db", BusyTimeoutSeconds = -3 }));
    }

    [Fact(DisplayName = "the sqlite connection string carries foreign keys, recursive triggers and a finite timeout, and honours the options")]
    public void The_sqlite_connection_string_is_complete()
    {
        var builder = new SqliteConnectionStringBuilder(SqliteProviderFactory.ConnectionStringOf(
            new OrmLiteOptions { Path = "x.db", BusyTimeoutSeconds = 7, Pooling = false }));

        Assert.Equal("x.db", builder.DataSource);
        Assert.True(builder.ForeignKeys);
        Assert.True(builder.RecursiveTriggers);
        Assert.Equal(7, builder.DefaultTimeout);
        Assert.False(builder.Pooling);
        Assert.Equal(SqliteOpenMode.ReadWriteCreate, builder.Mode);
        Assert.Equal(30, new SqliteConnectionStringBuilder(SqliteProviderFactory.ConnectionStringOf(new OrmLiteOptions { Path = "x.db" })).DefaultTimeout);
    }

    [Fact(DisplayName = "an explicit connection string is kept, and a timeout of zero in it is refused when the data source is gated")]
    public async Task An_explicit_connection_string_is_validated()
    {
        var kept = new SqliteConnectionStringBuilder(SqliteProviderFactory.ConnectionStringOf(
            new OrmLiteOptions { ConnectionString = "Data Source=y.db;Default Timeout=9;Foreign Keys=False" }));

        Assert.Equal(9, kept.DefaultTimeout);
        Assert.False(kept.ForeignKeys);
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", ConnectionString = "Data Source=z.db;Default Timeout=0" }));
        Assert.Contains("wait forever", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the postgres connection string is built from discrete settings, and options that do not apply are refused")]
    public void The_postgres_connection_string_is_built()
    {
        var builder = new NpgsqlConnectionStringBuilder(PostgresProviderFactory.ConnectionStringOf(
            new OrmLiteOptions { Host = "db.example", Port = 5433, Database = "app", Username = "u", Password = "p", Pooling = false }));

        Assert.Equal("db.example", builder.Host);
        Assert.Equal(5433, builder.Port);
        Assert.Equal("app", builder.Database);
        Assert.Equal("u", builder.Username);
        Assert.False(builder.Pooling);
        Assert.Throws<InvalidOperationException>(() => PostgresProviderFactory.ConnectionStringOf(new OrmLiteOptions { Host = "h", Path = "x" }));
        Assert.Throws<InvalidOperationException>(() => PostgresProviderFactory.ConnectionStringOf(new OrmLiteOptions { Host = "h", BusyTimeoutSeconds = 3 }));
        Assert.Contains("OrmLite:Host", Assert.Throws<InvalidOperationException>(() => PostgresProviderFactory.ConnectionStringOf(new OrmLiteOptions())).Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an explicit postgres connection string is the base, and a discrete setting overrides it")]
    public void Postgres_settings_override_the_connection_string()
    {
        var builder = new NpgsqlConnectionStringBuilder(PostgresProviderFactory.ConnectionStringOf(
            new OrmLiteOptions { ConnectionString = "Host=a;Database=one", Database = "two" }));

        Assert.Equal("a", builder.Host);
        Assert.Equal("two", builder.Database);
    }

    [Fact(DisplayName = "a postgres data source that disables the pool reset is refused by the postgres provider, and sqlite has no such rule")]
    public void The_reset_check_belongs_to_postgres()
    {
        using var source = new ConnectionStringSource("Host=h;No Reset On Close=true");

        var refusal = Assert.Throws<InvalidOperationException>(() => PostgresProvider.Instance.ValidateDataSource("request", source));

        Assert.Contains("No Reset On Close", refusal.Message, StringComparison.Ordinal);
        using var own = new SqliteDataSource("Data Source=a.db;Default Timeout=5", SqliteProvider.MinimumVersion);
        SqliteProvider.Instance.ValidateDataSource("request", own);
    }

    [Fact(DisplayName = "a context opened from options owns its data sources and answers a ping, and disposing it is safe twice")]
    public async Task An_opened_context_is_owned()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);

        Assert.True(await temp.Context.PingAsync());
        Assert.Equal("sqlite", temp.Context.Provider.Id);
        Assert.Null(temp.Context.Dialect);
        await temp.Context.DisposeAsync();
    }

    private sealed class ConnectionStringSource(string text) : System.Data.Common.DbDataSource
    {
        public override string ConnectionString => text;

        protected override System.Data.Common.DbConnection CreateDbConnection() => throw new NotSupportedException();
    }

    private sealed class ImpostorFactory : IProviderFactory
    {
        public string Id => "sqlite";

        public ISqlProvider CreateProvider(OrmLiteOptions options) => throw new NotSupportedException();

        public System.Data.Common.DbDataSource CreateDataSource(OrmLiteOptions options, DbLaneKind lane) => throw new NotSupportedException();
    }
}
