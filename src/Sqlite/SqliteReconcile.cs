using System.Globalization;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

internal sealed partial class SqliteMigrationUnit
{
    private async Task<IReadOnlyList<Statement>> ReconcileAsync(SqliteCatalog catalog, ReconcileIndexes node, CancellationToken ct)
    {
        var statements = new List<Statement>();
        foreach (var item in node.Indexes.Cast<ReconcileIndex>()) statements.AddRange(await ReconcileAsync(catalog, item, ct));
        return statements;
    }

    private async Task<IReadOnlyList<Statement>> ReconcileAsync(SqliteCatalog catalog, ReconcileIndex item, CancellationToken ct)
    {
        var table = item.Create.Table;
        if (!await catalog.TableExistsAsync(table, ct)) return [];
        var present = (await catalog.TableColumnsAsync(table, ct)).Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!item.GuardColumns.Cast<ColumnRef>().All(c => present.Contains(c.Column))) return [];
        if (item.CollisionKeys is not null) await RefuseCollisionAsync(catalog, item, ct);
        await RefuseDecimalTermAsync(catalog, item.Create, ct);
        var create = Compile(item.Create);
        var stored = (await catalog.ObjectsAsync(ct))
            .FirstOrDefault(o => o.Type == SqliteDataLiterals.IndexType && string.Equals(o.Name, item.Create.Index, StringComparison.OrdinalIgnoreCase));
        return stored is not null && Canonical(stored.Sql) != Canonical(create.Text)
            ? [Compile(new DropIndex(item.Create.Index, false)), create]
            : [create];
    }

    private async Task<IReadOnlyList<Statement>> CreateIndexAsync(SqliteCatalog catalog, CreateIndex node, CancellationToken ct)
    {
        await RefuseDecimalTermAsync(catalog, node, ct);
        return [Compile(node)];
    }

    private async Task RefuseDecimalTermAsync(SqliteCatalog catalog, CreateIndex node, CancellationToken ct)
    {
        var named = node.Terms.Select(DecimalTermColumn).OfType<string>().ToList();
        if (named.Count == 0) return;
        var table = SqliteCatalog.TableOf(await catalog.ObjectsAsync(ct), node.Table);
        if (table is null) return;
        var decimals = SqliteSqlText.ParseTable(table.Sql).Definitions
            .Where(d => d.Kind == DefinitionKind.Column && d.Tokens.Any(t => t.IsWord(SqliteDecimal.Collation)))
            .Select(d => d.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var refused = named.FirstOrDefault(decimals.Contains);
        if (refused is not null)
            throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, SqliteDataLiterals.DecimalTermMessage, node.Index, node.Table, refused));
    }

    private static string? DecimalTermColumn(Node term)
    {
        var collated = term is OrderTerm { Collation: not null };
        return (term is OrderTerm order ? order.Operand : term) switch
        {
            Call { Arguments: [ColumnRef lowered] } => lowered.Column,
            ColumnRef plain when collated => plain.Column,
            _ => null,
        };
    }

    private static string Canonical(string sql) =>
        string.Join(SqliteDataLiterals.WordSeparator, sql.Replace(SqliteDataLiterals.IfNotExists, "", StringComparison.OrdinalIgnoreCase).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private async Task RefuseCollisionAsync(SqliteCatalog catalog, ReconcileIndex item, CancellationToken ct)
    {
        var keys = item.CollisionKeys!.Cast<ColumnRef>().Select(c => c.Column).ToList();
        foreach (var index in await catalog.RowsAsync(new SqlitePragma("index_list", item.Create.Table), ct))
        {
            if (Convert.ToInt64(index[2], CultureInfo.InvariantCulture) == 0 || Convert.ToInt64(index[4], CultureInfo.InvariantCulture) != 0) continue;
            var members = (await catalog.RowsAsync(new SqlitePragma("index_info", (string)index[1]!), ct)).Select(m => (string?)m[2]).ToList();
            if (members.Count != keys.Count || members.Any(m => m is null || !keys.Contains(m, StringComparer.OrdinalIgnoreCase))) continue;
            var owner = (string)index[3]! == SqliteDataLiterals.ExplicitIndexOrigin ? SqliteDataLiterals.IndexOwner : SqliteDataLiterals.ConstraintOwner;
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture, SqliteDataLiterals.CollisionMessage,
                item.Create.Index, item.Create.Table, string.Join(SqliteDataLiterals.ListSeparator, keys), owner, (string)index[1]!));
        }
    }
}
