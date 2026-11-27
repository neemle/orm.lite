using Neemle.OrmLite.Nodes;
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

    private const string MainSchema = "main";

    // The table exactly as every other statement of the provider names it.
    private string QuotedTable(EntityDescriptor table) => CreateSerializer().Fragment(new TableRef(table.Table));
}
