using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.SqliteRules;

public sealed class SqliteGateSpellingTests
{
    private static Task<OrmContext> OpenAsync(string spelling) =>
        OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = spelling, BusyTimeoutSeconds = 1 });

    private static async Task AssertSharedGateAsync(TempSqlite first, string spelling)
    {
        await using var second = await OpenAsync(spelling);
        await using var holder = await UnitOfWork.BeginAsync(first.Context);

        var refusal = await Assert.ThrowsAsync<SqliteException>(() => UnitOfWork.BeginAsync(second));

        Assert.Contains("single-writer gate", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a relative path and the absolute path of one database file share the writer gate")]
    public async Task A_relative_spelling_shares_the_gate()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);

        await AssertSharedGateAsync(temp, Path.GetRelativePath(Directory.GetCurrentDirectory(), temp.Path_));
    }

    [Fact(DisplayName = "names that differ only in case share the writer gate only where the volume ignores case")]
    public async Task A_case_spelling_follows_the_volume()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);
        var upper = Path.Combine(Path.GetDirectoryName(temp.Path_)!, "DB.SQLITE");
        if (File.Exists(upper))
        {
            await AssertSharedGateAsync(temp, upper);
            return;
        }
        await using var other = await OpenAsync(upper);
        await using var holder = await UnitOfWork.BeginAsync(temp.Context);

        await using var free = await UnitOfWork.BeginAsync(other).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(free);
    }

    [Fact(DisplayName = "a symlink to the database file shares the writer gate with the file")]
    public async Task A_symlink_shares_the_gate()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);
        var link = temp.Path_ + ".link";
        try
        {
            File.CreateSymbolicLink(link, temp.Path_);
        }
        catch (Exception refusal)
            when (refusal is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            link = Path.Combine(Path.GetDirectoryName(temp.Path_)!, ".", "db.sqlite");
        }

        await AssertSharedGateAsync(temp, link);
    }

    [Fact(DisplayName = "a file: URI of the database file shares the writer gate with the path")]
    public async Task A_file_uri_shares_the_gate()
    {
        await using var temp = await TempSqlite.OpenAsync(o => o.BusyTimeoutSeconds = 1);

        await AssertSharedGateAsync(temp, new Uri(temp.Path_).AbsoluteUri + "?mode=rwc");
    }
}
