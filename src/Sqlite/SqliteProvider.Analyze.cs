using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider
{
    /// <summary><c>ANALYZE main</c>, or <c>ANALYZE main.&lt;table&gt;</c> quoted by the serializer.</summary>
    public string RenderAnalyze(AnalyzeScope scope, EntityDescriptor? table) => scope switch
    {
        AnalyzeScope.Database => new SqlWriter().Atom("ANALYZE").Atom(MainSchema).ToString(),
        AnalyzeScope.Table when table is not null =>
            new SqlWriter().Atom("ANALYZE").Atom(MainSchema).Dot().Attach(QuotedTable(table)).ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "not an ANALYZE scope with its target."),
    };
}
