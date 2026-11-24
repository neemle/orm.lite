using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.Tests.Corpus;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class NestedExistsTests
{
    public static TheoryData<string> Providers => new() { "postgres", "sqlite" };

    private static Query<CorpusTag> Tags(string provider)
    {
        CorpusWorld.Ensure();
        ISqlProvider chosen = provider == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;
        return new Query<CorpusTag>(
            new OrmContext(new UnreachableDataSource(), chosen),
            _ => throw new NotSupportedException("no rows are read here"));
    }

    [Theory(DisplayName = "Db.Exists nested three deep selects across four tables in one statement, each level correlated to the one above it and every alias distinct")]
    [MemberData(nameof(Providers))]
    public void Exists_nests_three_levels_deep_in_one_statement(string provider)
    {
        var sql = Tags(provider)
            .Where(t => Db.Exists<CorpusNote>(n => n.Id == t.NoteId
                && Db.Exists<CorpusProject>(p => p.Id == n.OwnerId
                    && Db.Exists<CorpusTask>(k => k.ProjectId == p.Id && k.Name == "x"))))
            .RenderSelect();

        Assert.Equal(3, sql.Split("EXISTS").Length - 1);
        Assert.Contains("s0.\"id\" = \"corpus_tags\".\"note_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("s1.\"id\" = s0.\"owner_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("s2.\"project_id\" = s1.\"id\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(" s3", sql, StringComparison.Ordinal);
    }
}
