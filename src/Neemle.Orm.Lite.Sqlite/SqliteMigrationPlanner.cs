using System.Data.Common;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

internal sealed class SqliteMigrationPlanner(SqliteProvider provider) : IMigrationPlanner
{
    public void Validate(IReadOnlyList<Node> operations)
    {
        foreach (var operation in operations)
        {
            DdlCompiler.Validate(operation, provider);
            if (operation is HistoryTableNode)
                throw new CapabilityNotSupportedException(provider.Id, operation.Name, Capability.RowHistoryTriggers);
            Check(operation);
        }
    }

    private void Check(Node node)
    {
        if (node is TypeRef { Kind: ColumnType.Decimal } decimalType && !SqliteDecimal.Accepts(decimalType))
            throw SqliteDecimal.Refusal(decimalType);
        if (node is OrderTerm { Collation: { } collation } && !SqliteDataLiterals.Collations.Split(SqliteDataLiterals.CollationSeparator).Contains(collation, StringComparer.OrdinalIgnoreCase))
            throw new NotSupportedException(string.Format(System.Globalization.CultureInfo.InvariantCulture, SqliteDataLiterals.CollationMessage, collation, SqliteDataLiterals.Collations));
        foreach (var child in node.Children()) Check(child);
    }

    public async ValueTask<IMigrationUnit> BeginUnitAsync(DbConnection connection, IReadOnlyList<Node> operations, CancellationToken ct)
    {
        var unit = new SqliteMigrationUnit(provider, connection, operations.Any(SqliteDispositions.MayRebuild));
        await unit.PrepareAsync(ct);
        return unit;
    }
}

internal sealed partial class SqliteMigrationUnit(SqliteProvider provider, DbConnection connection, bool foreignKeysOff) : IMigrationUnit
{
    private bool _off;

    internal async Task PrepareAsync(CancellationToken ct)
    {
        if (!foreignKeysOff) return;
        await RunPragmaAsync(new SqlitePragma("foreign_keys", null, "OFF"), ct);
        _off = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_off) return;
        _off = false;
        try
        {
            await RunPragmaAsync(new SqlitePragma("foreign_keys", null, "ON"), CancellationToken.None);
        }
        catch (DbException)
        {
        }
    }

    public async Task VerifyAsync(DbTransaction transaction, CancellationToken ct)
    {
        if (!_off) return;
        var rows = await new SqliteCatalog(provider, connection, transaction).RowsAsync(new SqlitePragma("foreign_key_check"), ct);
        if (rows.Count == 0) return;
        var first = rows[0];
        throw new InvalidOperationException(
            $"the migration leaves {rows.Count} row(s) that violate a foreign key, the first in table '{first[0]}' (row {first[1]}) against '{first[2]}'; nothing was changed.");
    }

    private async Task RunPragmaAsync(Node pragma, CancellationToken ct)
    {
        var statement = StatementCompiler.Compile(pragma, provider, StatementKind.Ddl);
        await using var command = connection.CreateCommand();
        command.CommandText = statement.Text;
        await command.ExecuteNonQueryAsync(ct);
    }

    private Statement Compile(Node node) => StatementCompiler.Compile(node, provider, StatementKind.Ddl);

    private List<Statement> Compile(IEnumerable<Node> nodes) => nodes.Select(Compile).ToList();

    public async Task<IReadOnlyList<Statement>> PlanAsync(Node operation, DbTransaction transaction, CancellationToken ct)
    {
        var catalog = new SqliteCatalog(provider, connection, transaction);
        return operation switch
        {
            AddColumn n => await AddColumnAsync(catalog, n, ct),
            DropColumn n => await DropColumnAsync(catalog, n, ct),
            DropTable n => await DropTableAsync(catalog, n, ct),
            DropIndex n => await DropIndexAsync(catalog, n, ct),
            CreateIndex n => await CreateIndexAsync(catalog, n, ct),
            CreateView n => PlanCreateView(n),
            DropView n => await DropViewAsync(catalog, n, ct),
            AddForeignKey n => await AddForeignKeyAsync(catalog, n, ct),
            DropForeignKey n => await DropForeignKeyAsync(catalog, n, ct),
            ConvertForeignKeyAction n => await ConvertForeignKeyAsync(catalog, n, ct),
            SoftDeleteColumn n => await SoftDeleteColumnAsync(catalog, n, ct),
            ConvertToLiveOnlyIndex n => await LiveOnlyIndexAsync(catalog, n, ct),
            ReconcileIndexes n => await ReconcileAsync(catalog, n, ct),
            ReconcileIndex n => await ReconcileAsync(catalog, n, ct),
            RowHistoryInstall n => await HistoryInstallAsync(catalog, n, ct),
            RowHistoryFunction => [],
            RenameColumn n => await RenameColumnAsync(catalog, n, ct),
            Script n => await ScriptAsync(n, transaction, ct),
            _ => [Compile(operation)],
        };
    }

    private async Task<IReadOnlyList<Statement>> ScriptAsync(Script script, DbTransaction transaction, CancellationToken ct)
    {
        var planned = new List<Statement>();
        foreach (var statement in script.Statements) planned.AddRange(await PlanAsync(statement, transaction, ct));
        return planned;
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<Statement>> AddColumnAsync(SqliteCatalog catalog, AddColumn node, CancellationToken ct)
    {
        var exists = (await catalog.TableColumnsAsync(node.Table, ct)).Any(c => Same(c.Name, node.Column.Column));
        if (node.IfNotExists && exists) return [];
        if (SqliteDispositions.NativeAddable(node.Column))
            return await WithHistoryAsync(catalog, node.Table, [Compile(node with { IfNotExists = false })], columns => [.. columns, node.Column.Column], null, ct);
        return await RebuildAsync(catalog, node.Table, edit => edit.AddColumn(node.Column), ct);
    }

    private async Task<IReadOnlyList<Statement>> DropColumnAsync(SqliteCatalog catalog, DropColumn node, CancellationToken ct)
    {
        var columns = await catalog.TableColumnsAsync(node.Table, ct);
        var column = columns.FirstOrDefault(c => Same(c.Name, node.Column));
        if (column is null) return node.IfExists ? [] : [Compile(node with { IfExists = false })];
        await RefuseConstraintBoundAsync(catalog, node, column, ct);
        var objects = await catalog.ObjectsAsync(ct);
        var plan = new List<Node>();
        foreach (var index in await catalog.IndexesAsync(node.Table, ct))
        {
            var sql = objects.First(o => o.Type == SqliteDataLiterals.IndexType && Same(o.Name, index.Name)).Sql;
            if (SqliteSqlText.MentionsInKeysOrPredicate(sql, node.Column)) plan.Add(new DropIndex(index.Name, false));
        }
        plan.Add(node with { IfExists = false });
        var statements = Compile(plan);
        statements.AddRange(await MarkerRemovalAsync(catalog, MarkerKey(SqliteDataLiterals.ColumnKind, node.Table + "." + node.Column), ct));
        return await WithHistoryAsync(
            catalog,
            node.Table,
            statements,
            columns => columns.Where(c => !Same(c, node.Column)).ToList(),
            spec => spec with { Unversioned = spec.Unversioned.Where(c => !Same(c, node.Column)).ToList() },
            ct);
    }

    private async Task RefuseConstraintBoundAsync(SqliteCatalog catalog, DropColumn node, TableColumn column, CancellationToken ct)
    {
        if (column.KeyPosition > 0)
            throw Bound(node, SqliteDataLiterals.BoundPrimaryKey);
        foreach (var index in await catalog.RowsAsync(new SqlitePragma("index_list", node.Table), ct))
        {
            if ((string)index[3]! == SqliteDataLiterals.ExplicitIndexOrigin) continue;
            var members = await catalog.RowsAsync(new SqlitePragma("index_info", (string)index[1]!), ct);
            if (members.Any(m => Same((string?)m[2] ?? "", node.Column)))
                throw Bound(node, SqliteDataLiterals.BoundUnique);
        }
        foreach (var table in (await catalog.ObjectsAsync(ct)).Where(o => o.Type == SqliteDataLiterals.TableType))
            foreach (var key in await catalog.RowsAsync(new SqlitePragma("foreign_key_list", table.Name), ct))
            {
                var own = Same(table.Name, node.Table) && Same((string)key[3]!, node.Column);
                var referenced = Same((string)key[2]!, node.Table) && key[4] is string target && Same(target, node.Column);
                if (own || referenced) throw Bound(node, SqliteDataLiterals.BoundForeignKey);
            }
    }

    private static NotSupportedException Bound(DropColumn node, string reason) =>
        new NotSupportedException($"column '{node.Column}' of table '{node.Table}' {reason}; SQLite cannot drop it implicitly as PostgreSQL does, so drop that constraint first.");

    private async Task<IReadOnlyList<Statement>> DropTableAsync(SqliteCatalog catalog, DropTable node, CancellationToken ct)
    {
        var statements = Compile([node]);
        statements.AddRange(await MarkerRemovalAsync(catalog, MarkerKey(SqliteDataLiterals.ColumnKind, node.Table + "." + SoftDeleteColumnDdl.Column), ct));
        statements.AddRange(await MarkerRemovalAsync(catalog, HistoryKey(node.Table), ct));
        return statements;
    }

    private async Task<IReadOnlyList<Statement>> DropIndexAsync(SqliteCatalog catalog, DropIndex node, CancellationToken ct)
    {
        var statements = Compile([node]);
        statements.AddRange(await MarkerRemovalAsync(catalog, MarkerKey(SqliteDataLiterals.IndexKind, node.Index), ct));
        return statements;
    }

    private IReadOnlyList<Statement> PlanCreateView(CreateView node)
    {
        var plan = new List<Node>();
        if (node.Replace) plan.Add(new DropView(node.View));
        plan.Add(new SqliteStored(provider.CreateSerializer().Serialize(node with { Replace = false }).Text));
        plan.AddRange(RememberMarker(MarkerKey(SqliteDataLiterals.ViewKind, node.View), ViewDefinitions.Marker(provider, node)));
        return Compile(plan);
    }

    private async Task<IReadOnlyList<Statement>> DropViewAsync(SqliteCatalog catalog, DropView node, CancellationToken ct)
    {
        var statements = Compile([node]);
        statements.AddRange(await MarkerRemovalAsync(catalog, MarkerKey(SqliteDataLiterals.ViewKind, node.View), ct));
        return statements;
    }

    private static ForeignKeyDef DefinitionOf(AddForeignKey node) =>
        new(node.Constraint, node.Columns, node.ReferencedTable, node.ReferencedColumns, node.OnDelete, node.OnUpdate);

    private Task<IReadOnlyList<Statement>> AddForeignKeyAsync(SqliteCatalog catalog, AddForeignKey node, CancellationToken ct) =>
        RebuildAsync(catalog, node.Table, edit => edit.AddConstraint(DefinitionOf(node)), ct);

    private Task<IReadOnlyList<Statement>> DropForeignKeyAsync(SqliteCatalog catalog, DropForeignKey node, CancellationToken ct) =>
        RebuildAsync(catalog, node.Table, edit => edit.Remove(edit.RequireForeignKey(node.Constraint)), ct);

    private async Task<IReadOnlyList<Statement>> ConvertForeignKeyAsync(SqliteCatalog catalog, ConvertForeignKeyAction node, CancellationToken ct)
    {
        var tableObject = SqliteCatalog.TableOf(await catalog.ObjectsAsync(ct), node.Table);
        if (tableObject is null) return [];
        var current = SqliteSqlText.ParseTable(tableObject.Sql).Definitions
            .Where(d => SqliteSqlText.IsForeignKey(d) && d.Name is not null && Same(d.Name, node.Constraint))
            .Select(SqliteSqlText.ParseForeignKey)
            .FirstOrDefault();
        if (current is null || current.OnDelete == node.Target) return [];
        var replacement = new ForeignKeyDef(node.Constraint, node.Columns, node.ReferencedTable, node.ReferencedColumns, node.Target, node.OnUpdate);
        return await RebuildAsync(catalog, node.Table, edit => edit.Replace(edit.RequireForeignKey(node.Constraint), replacement), ct);
    }
}
