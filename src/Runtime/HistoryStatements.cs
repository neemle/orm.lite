#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class HistoryStatements
{
    internal const string SoftDeleteOp = "soft_delete";

    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    internal static string Name(string identifier) =>
        Identifier.IsMatch(identifier) ? identifier : throw new ArgumentException("not an identifier: " + identifier);

    internal static Statement Compile(ISqlProvider provider, Node tree, StatementKind kind = StatementKind.Read) =>
        StatementCompiler.Compile(tree, provider, kind);

    internal static DbCommand Command(DbConnection connection, DbTransaction? transaction, Statement statement)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = statement.Text;
        cmd.Transaction = transaction;
        Bind(cmd, statement);
        return cmd;
    }

    internal static void Bind(DbCommand cmd, Statement statement)
    {
        foreach (var parameter in statement.Parameters) cmd.AddParam(parameter.Name, parameter.Value);
    }

    private static ColumnRef H(string column, string? owner = null) =>
        new(column, owner is null ? null : new AliasRef(owner), true);

    private static ColumnRef Q(string column, string? owner = null) =>
        new(column, owner is null ? null : new AliasRef(owner));

    private static TableRef HistoryTable(string? alias = null) => new(RowHistoryDdl.TableName, alias, true);

    private static Compare Eq(Node left, Node right) => new(CompareOp.Equal, left, right);

    private static Param P(int ordinal, string alias, object? value) => new(ordinal, value, alias);

    private static ColumnRef Column(string name) => new(Name(name));

    private static NodeList Columns(IEnumerable<string> names) => NodeList.Of(names.Select(n => (Node)Column(n)));

    private static Exists ScopeExists(string table, string keyColumn, string tenantColumn, Param row, Param tenant) =>
        new(new Select(
            [new IntLit(1)],
            new TableRef(table, "r"),
            new Where([Eq(Q(keyColumn, "r"), row), Eq(Q(tenantColumn, "r"), tenant)])));

    internal static Node RowExists(string table, string keyColumn, string? tenantColumn, Guid row, Guid? tenant)
    {
        foreach (var name in new[] { table, keyColumn }.Concat(tenantColumn is null ? [] : [tenantColumn])) Name(name);
        var id = P(0, "id", row);
        var terms = new List<Node> { Eq(Q(keyColumn, "r"), id) };
        if (tenantColumn is not null) terms.Add(Eq(Q(tenantColumn, "r"), P(1, "w", tenant)));
        var inner = new Select([new IntLit(1)], new TableRef(table, "r"), new Where(NodeList.Of(terms)));
        return new Select([new Exists(inner)]);
    }

    internal static Node Versions(
        string table, string? keyColumn, string? tenantColumn, string sourceTable, Guid row, Guid? tenant)
    {
        var t = P(0, "t", sourceTable);
        var id = P(1, "id", row);
        var terms = new List<Node> { Eq(H("source_table"), t), Eq(H("row_id"), id) };
        if (tenantColumn is not null)
        {
            foreach (var name in new[] { table, keyColumn ?? "", tenantColumn }) Name(name);
            terms.Add(ScopeExists(table, keyColumn!, tenantColumn, id, P(2, "w", tenant)));
        }
        return new Select(
            [
                H("id"), H("op"), H("actor_user_id"), H("actor_kind"), new HistoryChangedKeys(H("old_values")),
                H("xact_id"), H("changed_at"),
            ],
            HistoryTable(),
            new Where(NodeList.Of(terms)),
            new OrderBy([new OrderTerm(H("id"), true)]));
    }

    internal static HistoryStateQuery State(
        string table, string keyColumn, IReadOnlyList<string> unversioned, string? tenantColumn,
        IReadOnlyList<string> columns, string sourceTable, Guid row, long version, Guid? tenant)
    {
        var named = new[] { table, keyColumn }.Concat(unversioned).Concat(tenantColumn is null ? [] : [tenantColumn]);
        foreach (var name in named) Name(name);
        return new HistoryStateQuery(
            table, keyColumn, tenantColumn, NodeList.Of(columns.Select(c => (Node)new ColumnRef(c))),
            Columns(unversioned), P(0, "t", sourceTable), P(1, "id", row), P(2, "k", version),
            tenantColumn is null ? null : P(3, "w", tenant));
    }

    internal static Node Revert(
        ISoftEntry entry, bool includeStatus, string sourceTable, Guid row, long version, Guid? tenant)
    {
        var reverted = RowHistoryRevert.RevertedColumns(entry, includeStatus);
        var all = OrmMeta.Get(entry.ClrType).Columns;
        var hasStamp = all.Contains(RowHistoryRevert.UpdateStamp, StringComparer.Ordinal);
        var stamp = hasStamp ? RowHistoryRevert.UpdateStamp : null;
        if (reverted.Count == 0 && stamp is null)
            throw new InvalidOperationException("nothing to revert on " + entry.Table);
        var tenantColumn = HistoryScope.Column("RevertAsync", entry);
        var state = State(
            entry.Table, entry.KeyColumn, entry.Unversioned, tenantColumn, all, sourceTable, row, version, tenant);
        return new HistoryRevertUpdate(state, entry.SoftColumn, Columns(reverted), stamp);
    }

    internal static Node LockRow(ISoftEntry entry, Guid row, Guid? tenant)
    {
        var tenantColumn = HistoryScope.Column("LockAsync", entry);
        var project = SoftTrashRead.ProjectColumnOf(entry);
        var terms = new List<Node> { Eq(Column(entry.KeyColumn), P(0, "id", row)) };
        if (tenantColumn is not null) terms.Add(Eq(Column(tenantColumn), P(1, "w", tenant)));
        return new Select(
            [
                new IsNotNull(Column(entry.SoftColumn)),
                project is null ? new Cast(new NullLit(), CastType.Uuid) : Column(project),
            ],
            new TableRef(entry.Table),
            new Where(NodeList.Of(terms)),
            Lock: new RowLock(RowLockStrength.Update));
    }

    internal static Node NewestVersion(ISoftEntry entry, Guid row, Guid? tenant)
    {
        var id = P(1, "id", row);
        var terms = new List<Node> { Eq(H("source_table"), P(0, "t", entry.Table)), Eq(H("row_id"), id) };
        if (Scope(entry, "NewestVersionAsync", id, 2, tenant) is { } scope) terms.Add(scope);
        return new Select([new Call(Fn.Max, [H("id")])], HistoryTable(), new Where(NodeList.Of(terms)));
    }

    internal static Node VersionExists(ISoftEntry entry, Guid row, long version, Guid? tenant)
    {
        var id = P(1, "id", row);
        var terms = new List<Node>
        {
            Eq(H("id"), P(2, "k", version)), Eq(H("source_table"), P(0, "t", entry.Table)), Eq(H("row_id"), id),
        };
        if (Scope(entry, "VersionExistsAsync", id, 3, tenant) is { } scope) terms.Add(scope);
        return new Select([new Exists(new Select([new IntLit(1)], HistoryTable(), new Where(NodeList.Of(terms))))]);
    }

    internal static Node LatestVersion(ISoftEntry entry, Guid row, Guid? tenant)
    {
        var id = P(1, "id", row);
        var terms = new List<Node> { Eq(H("source_table"), P(0, "t", entry.Table)), Eq(H("row_id"), id) };
        if (Scope(entry, "PlanAsync", id, 2, tenant) is { } scope) terms.Add(scope);
        return new Select(
            [H("op"), H("xact_id"), H("changed_at")],
            HistoryTable(),
            new Where(NodeList.Of(terms)),
            new OrderBy([new OrderTerm(H("id"), true)]),
            new Limit(1));
    }

    private static Node? Scope(ISoftEntry entry, string verb, Param row, int ordinal, Guid? tenant)
    {
        if (HistoryScope.Column(verb, entry) is not { } column) return null;
        foreach (var name in new[] { entry.Table, entry.KeyColumn, column }) Name(name);
        return ScopeExists(entry.Table, entry.KeyColumn, column, row, P(ordinal, "w", tenant));
    }

    internal static Node BatchMembers(Guid? tenant, long xact, DateTime changedAt) =>
        new Select(
            [H("source_table", "h"), H("row_id", "h")],
            HistoryTable("h"),
            new Where(
            [
                new NullSafeEquals(H("tenant_id", "h"), new Cast(P(0, "w", tenant), CastType.Uuid)),
                Eq(H("xact_id", "h"), P(1, "x", xact)),
                Eq(H("changed_at", "h"), P(2, "c", changedAt)),
                Eq(H("op", "h"), new StringLit(SoftDeleteOp)),
                new Exists(
                    new Select(
                        [new IntLit(1)],
                        HistoryTable("h2"),
                        new Where(
                        [
                            Eq(H("source_table", "h2"), H("source_table", "h")),
                            Eq(H("row_id", "h2"), H("row_id", "h")),
                            new Compare(CompareOp.Greater, H("id", "h2"), H("id", "h")),
                        ])),
                    true),
            ]));

    internal static Node ShareLock(ISoftEntry entry, Guid key, Guid? tenant)
    {
        var terms = new List<Node> { Eq(Column(entry.KeyColumn), P(0, "k", key)) };
        if (HistoryScope.Column("IsDeletedUnderShareLockAsync", entry) is { } tenantColumn)
            terms.Add(Eq(Column(tenantColumn), P(1, "w", tenant)));
        return new Select(
            [new IsNotNull(Column(entry.SoftColumn))],
            new TableRef(entry.Table),
            new Where(NodeList.Of(terms)),
            Lock: new RowLock(RowLockStrength.Share));
    }

    internal static Node LockDeleted(
        ISoftEntry entry, IReadOnlyList<string> selected, IReadOnlyCollection<Guid> ids, Guid? tenant)
    {
        var terms = new List<Node> { new AnyOf(Column(entry.KeyColumn), P(0, "ids", ids.ToArray())) };
        if (HistoryScope.Column("PlanAsync", entry) is { } tenantColumn)
            terms.Add(Eq(Column(tenantColumn), P(1, "w", tenant)));
        terms.Add(new IsNotNull(Column(entry.SoftColumn)));
        return new Select(
            NodeList.Of(selected.Select(c => (Node)Column(c))),
            new TableRef(entry.Table),
            new Where(NodeList.Of(terms)),
            new OrderBy([Column(entry.KeyColumn)]),
            Lock: new RowLock(RowLockStrength.Update));
    }

    internal static Node ExistingTables(IReadOnlyList<string> tables) =>
        new ExistingTablesQuery(P(0, "tables", tables.ToArray()));

    internal static Node SoftDeletes(IReadOnlyList<string> tables, IReadOnlyList<Guid> ids) =>
        new HistorySoftDeleteLookup(P(0, "tables", tables.ToArray()), P(1, "ids", ids.ToArray()));

    internal static Node ConstraintColumns(ConstraintSource source, string name) =>
        new ConstraintColumnsOf(source, P(0, "n", name));

    internal static Node? TrashUnion(
        IReadOnlyList<ISoftEntry> entries, SoftTrashFilter filter, Guid tenant, int limit, int offset)
    {
        var branches = new List<Node>();
        foreach (var entry in entries)
            if (TrashBranch(entry, filter, tenant) is { } branch) branches.Add(branch);
        if (branches.Count == 0) return null;
        return new SoftTrashUnion(NodeList.Of(branches), P(2, "lim", limit), P(3, "off", offset));
    }

    private static bool TrashListed(ISoftEntry entry, SoftTrashFilter filter)
    {
        if (entry.KeyColumn.Length == 0) return false;
        if (filter.EntityToken is { } wanted && !string.Equals(entry.EntityToken, wanted, StringComparison.Ordinal))
            return false;
        if (entry.Platform) return filter.IncludePlatform;
        if (entry.TenantColumn is not { } tenantColumn) return false;
        return filter.IncludePlatform || !string.Equals(tenantColumn, entry.KeyColumn, StringComparison.Ordinal);
    }

    private static Node? TrashBranch(ISoftEntry entry, SoftTrashFilter filter, Guid tenant)
    {
        if (!TrashListed(entry, filter)) return null;

        Node? tenantPredicate = entry.Platform ? null : Eq(Column(entry.TenantColumn!), P(0, "w", tenant));
        var projectColumn = SoftTrashRead.ProjectColumnOf(entry);
        if (filter.ProjectId is not null && projectColumn is null) return null;

        var terms = new List<Node>();
        if (tenantPredicate is not null) terms.Add(tenantPredicate);
        terms.Add(new IsNotNull(Column(entry.SoftColumn)));
        if (filter.ProjectId is { } project) terms.Add(Eq(Column(projectColumn!), P(1, "p", project)));

        Name(entry.EntityToken);
        return new SoftTrashBranch(
            entry.EntityToken, Name(entry.Table), Name(entry.KeyColumn),
            entry.LabelColumn is { } label ? Name(label) : null,
            Name(entry.SoftColumn), projectColumn is { } c ? Name(c) : null,
            new Where(NodeList.Of(terms)));
    }
}
