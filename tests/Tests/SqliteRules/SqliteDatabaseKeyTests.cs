using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.SqliteRules;

public sealed class SqliteDatabaseKeyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ormlite-key-" + Guid.NewGuid().ToString("N"));

    public SqliteDatabaseKeyTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private string At(string name) => Path.Combine(_root, name);

    private static string? Key(string source, string? dataDirectory = null) =>
        SqliteDatabaseKey.Of("Data Source=" + source, dataDirectory);

    private static SqliteDataSource Source(string connectionString) =>
        new(connectionString, SqliteProvider.MinimumVersion);

    private bool VolumeIgnoresCase()
    {
        File.WriteAllText(At("probe.tmp"), "x");
        return File.Exists(At("PROBE.TMP"));
    }

    private static bool TryLink(string link, string target, bool directory)
    {
        try
        {
            if (directory) Directory.CreateSymbolicLink(link, target);
            else File.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception refusal)
            when (refusal is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    [Theory(DisplayName = "two spellings that differ only in case share a key exactly where the volume ignores case")]
    [InlineData(true)]
    [InlineData(false)]
    public void Case_spellings_share_a_key_where_the_volume_ignores_case(bool exists)
    {
        var ignores = VolumeIgnoresCase();
        var lower = At("notes.db");
        var upper = At("NOTES.DB");
        if (exists)
        {
            File.WriteAllText(lower, "x");
            if (!ignores) File.WriteAllText(upper, "y");
        }

        Assert.Equal(ignores, Key(lower) == Key(upper));
    }

    [Fact(DisplayName = "different files keep different keys")]
    public void Different_files_keep_different_keys()
    {
        Assert.NotEqual(Key(At("one.db")), Key(At("two.db")));
        Assert.NotEqual(Key(At("one.db")), Key(At(Path.Combine("sub", "one.db"))));
    }

    [Fact(DisplayName = "a symlink to a database file, a chain of links and a relative-target link key as the file")]
    public void A_symlink_keys_as_its_target()
    {
        var real = At("real.db");
        File.WriteAllText(real, "x");
        var first = At("first.db");
        var second = At("second.db");
        if (!TryLink(first, real, false) || !TryLink(second, "first.db", false))
        {
            Assert.Equal(Key(real), Key(At(Path.Combine(".", "real.db"))));
            return;
        }

        Assert.Equal(Key(real), Key(first));
        Assert.Equal(Key(real), Key(second));
        Assert.NotEqual(Path.GetFullPath(first), Key(first));
    }

    [Fact(DisplayName = "a path under a symlinked directory keys as the path under the real directory")]
    public void A_symlinked_directory_keys_as_its_target()
    {
        var real = Directory.CreateDirectory(At("real")).FullName;
        var link = At("link");
        if (!TryLink(link, real, true))
        {
            Assert.Equal(Key(Path.Combine(real, "a.db")), Key(Path.Combine(real, ".", "a.db")));
            return;
        }

        Assert.Equal(Key(Path.Combine(real, "a.db")), Key(Path.Combine(link, "a.db")));
        File.WriteAllText(Path.Combine(real, "a.db"), "x");
        Assert.Equal(Key(Path.Combine(real, "a.db")), Key(Path.Combine(link, "a.db")));
    }

    [Fact(DisplayName = "a link to a missing file keys as the missing file, and a loop of links keys as written")]
    public void Dangling_and_looping_links_do_not_fail()
    {
        var missing = At("missing.db");
        var dangling = At("dangling.db");
        var loopA = At("a.lnk");
        var loopB = At("b.lnk");
        if (!TryLink(dangling, missing, false) || !TryLink(loopA, loopB, false) || !TryLink(loopB, loopA, false))
        {
            Assert.NotEqual(Key(missing), Key(At("other.db")));
            return;
        }

        Assert.Equal(Key(missing), Key(dangling));
        Assert.Equal(Path.GetFullPath(loopA), Key(loopA), ignoreCase: true);
    }

    [Fact(DisplayName = "the data directory macro, a relative name under it and the absolute path key alike")]
    public void The_data_directory_is_expanded()
    {
        var absolute = Key(At("a.db"));

        Assert.Equal(absolute, Key("|DataDirectory|a.db", _root));
        Assert.Equal(absolute, Key("a.db", _root));
        Assert.Equal(absolute, Key(At("a.db"), Path.Combine(_root, "elsewhere")));
        Assert.Equal(Key(At(Path.Combine("sub", "a.db"))), Key("|DataDirectory|" + Path.Combine("sub", "a.db"), _root));
    }

    [Fact(DisplayName = "without a data directory the macro is an ordinary name, as in Microsoft.Data.Sqlite")]
    public void Without_a_data_directory_the_macro_stays_literal()
    {
        Assert.Equal(Key(Path.GetFullPath("|DataDirectory|a.db")), Key("|DataDirectory|a.db"));
        Assert.NotEqual(Key(At("a.db")), Key("|DataDirectory|a.db"));
    }

    [Fact(DisplayName = "file: URIs in their usual spellings key as the path they name")]
    public void File_uris_key_as_their_path()
    {
        var path = At("uri target.db");
        var uri = new Uri(path);

        Assert.Equal(Key(path), Key(uri.AbsoluteUri));
        Assert.Equal(Key(path), Key(uri.AbsoluteUri + "?mode=rwc"));
        Assert.Equal(Key(path), Key(uri.AbsoluteUri + "?mode=rwc&cache=private#fragment"));
        Assert.Equal(Key(path), Key("file://localhost" + uri.AbsolutePath));
        Assert.Equal(Key(path), Key("file:" + uri.AbsolutePath));
        Assert.Equal(Key(path), Key(uri.AbsoluteUri, Path.Combine(_root, "elsewhere")));
    }

    [Theory(DisplayName = "file: URIs of in-memory databases key by name when shared, else not at all")]
    [InlineData("file:shared?mode=memory&cache=shared", "memory:shared")]
    [InlineData("file:shared?cache=shared&mode=memory", "memory:shared")]
    [InlineData("file::memory:?cache=shared", "memory::memory:")]
    [InlineData("file:private?mode=memory", null)]
    [InlineData("file::memory:", null)]
    [InlineData("file:", null)]
    [InlineData("file://remote/x/a.db", null)]
    public void Memory_and_unopenable_uris(string source, string? expected) =>
        Assert.Equal(expected, Key(source));

    [Fact(DisplayName = "in-memory and empty sources still have no gate key, a shared one keeps its name")]
    public void Memory_keys_are_unchanged()
    {
        Assert.Null(Key(":memory:", _root));
        Assert.Null(Key("x;Mode=Memory", _root));
        Assert.Null(SqliteDatabaseKey.Of("Data Source=", _root));
        Assert.Equal("memory:shared", Key("shared;Mode=Memory;Cache=Shared", _root));
    }

    [Fact(DisplayName = "a data source fixes a relative name to an absolute path when created, others stay as given")]
    public async Task A_data_source_pins_its_path()
    {
        await using var relative = Source("Data Source=pinned-rel.db;Pooling=False");
        await using var memory = Source("Data Source=:memory:");
        await using var uri = Source("Data Source=file:x?mode=memory&cache=shared");

        var pinned = new SqliteConnectionStringBuilder(relative.ConnectionString);

        Assert.Equal(Path.GetFullPath("pinned-rel.db"), pinned.DataSource);
        Assert.False(pinned.Pooling);
        Assert.Equal("Data Source=:memory:", memory.ConnectionString);
        Assert.Equal("Data Source=file:x?mode=memory&cache=shared", uri.ConnectionString);
    }

    [Fact(DisplayName = "a connection made by the data source opens the pinned absolute path, not the raw name")]
    public async Task A_connection_uses_the_pinned_path()
    {
        await using var relative = Source("Data Source=pinned-conn.db");
        await using var connection = relative.CreateConnection();

        Assert.Equal(Path.GetFullPath("pinned-conn.db"), connection.DataSource);
    }

    [Theory(DisplayName = "a relative file: URI is made absolute when the data source is created, tail kept")]
    [InlineData("file:uri-rel.db", "uri-rel.db", "")]
    [InlineData("file:uri-rel.db?mode=rwc#frag", "uri-rel.db", "?mode=rwc#frag")]
    [InlineData("file:a%23b.db", "a#b.db", "")]
    [InlineData("file:a%25b.db?cache=private", "a%b.db", "?cache=private")]
    public async Task A_relative_uri_is_pinned(string uri, string name, string tail)
    {
        await using var source = Source("Data Source=" + uri);

        var pinned = new SqliteConnectionStringBuilder(source.ConnectionString).DataSource;

        Assert.StartsWith("file:", pinned, StringComparison.Ordinal);
        Assert.EndsWith(tail, pinned, StringComparison.Ordinal);
        Assert.Equal(Key(Path.GetFullPath(name)), Key(pinned));
    }

    [Theory(DisplayName = "an absolute, memory or empty file: URI is left exactly as given")]
    [InlineData("file:///tmp/x.db")]
    [InlineData("file:/tmp/x.db?mode=ro")]
    [InlineData("file:shared?mode=memory&cache=shared")]
    [InlineData("file::memory:?cache=shared")]
    [InlineData("file:")]
    public void Other_uris_are_not_rewritten(string uri)
    {
        var given = "Data Source=" + uri;

        Assert.Equal(given, SqliteDatabaseKey.Pin(given, null));
    }

    [Theory(DisplayName = "case folding lower-cases the path on a case-insensitive volume, else leaves it")]
    [InlineData(true, "/tmp/a/notes.db")]
    [InlineData(false, "/Tmp/A/Notes.DB")]
    public void Folding_follows_the_volume(bool insensitive, string expected)
    {
        Assert.Equal(expected, SqliteFileIdentity.Fold("/Tmp/A/Notes.DB", insensitive));
    }

    [Fact(DisplayName = "a NUL escape ends the path of a file: URI, as SQLite reads it")]
    public void A_nul_escape_ends_the_uri_path()
    {
        Assert.Equal(Key("file:a"), Key("file:a%00b.db"));
        Assert.Equal(Key("file:a"), Key("file:a%00b.db?mode=rwc"));
    }

    [Fact(DisplayName = "the key of a data source is that of its pinned path, is stable and is absent for memory")]
    public async Task A_data_source_has_one_key()
    {
        await using var relative = Source("Data Source=pinned-key.db");
        await using var memory = Source("Data Source=:memory:");

        Assert.Equal(SqliteDatabaseKey.Of(relative.ConnectionString), SqliteDatabaseKey.Of(relative));
        Assert.Same(SqliteDatabaseKey.Of(relative), SqliteDatabaseKey.Of(relative));
        Assert.Null(SqliteDatabaseKey.Of(memory));
    }

    [Fact(DisplayName = "pinning expands the data directory macro into the absolute path")]
    public void Pinning_expands_the_macro()
    {
        var pinned = new SqliteConnectionStringBuilder(SqliteDatabaseKey.Pin("Data Source=|DataDirectory|a.db", _root));

        Assert.Equal(At("a.db"), pinned.DataSource);
    }
}
