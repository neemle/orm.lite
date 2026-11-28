using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresProvider
{
    /// <summary><c>ANALYZE</c>, or <c>ANALYZE &lt;table&gt;</c> quoted by the serializer.</summary>
    public string RenderAnalyze(AnalyzeScope scope, EntityDescriptor? table) => scope switch
    {
        AnalyzeScope.Database => new SqlWriter().Atom("ANALYZE").ToString(),
        AnalyzeScope.Table when table is not null => new SqlWriter().Atom("ANALYZE").Atom(QuotedTable(table)).ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "not an ANALYZE scope with its target."),
    };
}
