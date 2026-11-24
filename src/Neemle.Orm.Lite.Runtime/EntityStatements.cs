#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public static class EntityStatements
{
    private const string InsertPrefix = "p";

    private const string SetPrefix = "s";

    private const string KeyAlias = "k";

    private const string UpdateKeyAlias = "key";

    private const string ThisAlias = "a";

    private const string OtherAlias = "b";

    private const string OtherTableAlias = "o";

    private const string JoinTableAlias = "j";

    public static Node Insert(EntityDescriptor d, IReadOnlyList<object?> values)
    {
        RequireTable(d);
        RequireWidth(d, values);
        var cells = d.Columns.Select((c, i) => Cell(c, new Param(i, values[i], InsertPrefix + i))).ToList();
        return InsertOf(d, cells);
    }

    public static InvalidOperationException ReadOnlyView(string table) =>
        new(table + " is a view; a view is read-only, so no insert, update or delete can be built on it.");

    private static void RequireTable(EntityDescriptor d)
    {
        if (d.IsView) throw ReadOnlyView(d.Table);
    }

    private static Insert InsertOf(EntityDescriptor d, List<Node> cells) =>
        new(new TableRef(d.Table), ColumnList(d.Columns.Select(c => c.Column)), [new ValuesRow(NodeList.Of(cells))]);

    public static Node Get(EntityDescriptor d, object? key)
    {
        var column = RequireKey(d);
        return new Select([new Star()], new TableRef(d.Table), new Where(NodeList.Of(KeyTerms(d, column, new Param(0, key, KeyAlias)))));
    }

    public static Node Update(EntityDescriptor d, IReadOnlyList<object?> values)
    {
        RequireTable(d);
        RequireWidth(d, values);
        var key = RequireKey(d);
        var set = d.SetColumns;
        if (set.Count == 0)
            throw new InvalidOperationException(d.ClrType + " has no column outside its key to update.");
        var assignments = new List<Node>(set.Count);
        for (var i = 0; i < set.Count; i++)
            assignments.Add(new Assignment(new ColumnRef(set[i].Column), Cell(set[i], new Param(i, ValueOf(d, values, set[i]), SetPrefix + i))));
        var where = new Where(NodeList.Of(KeyTerms(d, key, new Param(set.Count, ValueOf(d, values, key), UpdateKeyAlias))));
        return new Update(new TableRef(d.Table), NodeList.Of(assignments), where);
    }

    public static Node Delete(EntityDescriptor d, object? key)
    {
        RequireTable(d);
        var column = RequireKey(d);
        var where = new Where(NodeList.Of(KeyTerms(d, column, new Param(0, key, KeyAlias))));
        return d.Soft is null
            ? new Delete(new TableRef(d.Table), where)
            : new Update(new TableRef(d.Table), [new Assignment(new ColumnRef(d.Soft.Column), new ClockNow())], where);
    }

    public static Node LoadLinked(EntityDescriptor d, ManyToManyDescriptor link, object? thisKey)
    {
        var terms = new List<Node> { new Compare(CompareOp.Equal, Of(JoinTableAlias, link.ThisKey), new Param(0, thisKey, KeyAlias)) };
        if (link.OtherSoftColumn is { } soft) terms.Add(new IsNull(Of(OtherTableAlias, soft)));
        var join = new InnerJoin(
            new TableRef(link.JoinTable, JoinTableAlias),
            new Compare(CompareOp.Equal, Of(JoinTableAlias, link.OtherKey), Of(OtherTableAlias, link.OtherKeyColumn)));
        return new Select(
            [new Star(OtherTableAlias)],
            new TableRef(link.OtherTable, OtherTableAlias),
            new Where(NodeList.Of(terms)),
            Joins: [join]);
    }

    public static Node AddLink(ManyToManyDescriptor link, object? thisId, object? otherId) =>
        new Insert(
            new TableRef(link.JoinTable),
            ColumnList([link.ThisKey, link.OtherKey]),
            [new ValuesRow([new Param(0, thisId, ThisAlias), new Param(1, otherId, OtherAlias)])],
            new Conflict(NodeList.Empty));

    public static Node RemoveLink(ManyToManyDescriptor link, object? thisId, object? otherId) =>
        new Delete(
            new TableRef(link.JoinTable),
            new Where([
                new Compare(CompareOp.Equal, new ColumnRef(link.ThisKey), new Param(0, thisId, ThisAlias)),
                new Compare(CompareOp.Equal, new ColumnRef(link.OtherKey), new Param(1, otherId, OtherAlias)),
            ]));

    public static IReadOnlyList<Node> CreateTables(EntityDescriptor d)
    {
        var created = new List<Node>();
        if (!d.Managed) return created;
        created.Add(new CreateTable(d.Table, NodeList.Of(d.Columns.Select(Definition))));
        return created;
    }

    public static IReadOnlyList<Node> CreateJoinTables(EntityDescriptor d)
    {
        var created = new List<Node>();
        if (!d.Managed) return created;
        foreach (var link in d.ManyToMany.Where(l => l.Pure))
            created.Add(new CreateTable(link.JoinTable, [
                new ColumnDef(link.ThisKey, new TypeRef(ColumnType.Uuid), false),
                new ColumnDef(link.OtherKey, new TypeRef(ColumnType.Uuid), false),
                new TablePrimaryKey(ColumnList([link.ThisKey, link.OtherKey])),
            ]));
        return created;
    }

    public static IReadOnlyList<CreateIndex> CreateIndexes(EntityDescriptor d) =>
        d.Managed ? d.Indexes.Select(i => CreateIndex(d, i)).ToList() : [];

    public static IReadOnlyList<ReconcileIndex> ReconcileIndexes(EntityDescriptor d)
    {
        var items = new List<ReconcileIndex>();
        if (!d.Managed) return items;
        foreach (var index in d.Indexes)
        {
            var live = IsLive(d, index);
            var guard = index.Columns.Concat(index.Filter.Select(f => f.Column)).Concat(live ? [d.Soft!.Column] : []).Distinct(StringComparer.Ordinal);
            items.Add(new ReconcileIndex(CreateIndex(d, index), ColumnList(guard), live && index.IsKey ? ColumnList(index.Columns) : null));
        }
        return items;
    }

    private static bool IsLive(EntityDescriptor d, IndexDescriptor index) => index.LiveOnly && d.Soft is not null;

    private const string DecimalTermMessage =
        "Index '{0}' on '{1}': column '{2}' is a decimal, which is ordered and compared as a number, so a collation or a lowered term on it is refused; index the column as it is.";

    private static CreateIndex CreateIndex(EntityDescriptor d, IndexDescriptor index)
    {
        foreach (var term in index.Terms.Where(t => !t.IsKey))
            if (d.Columns.Any(c => c.Type == ColumnType.Decimal && c.Column == term.Column))
                throw new NotSupportedException(string.Format(System.Globalization.CultureInfo.InvariantCulture, DecimalTermMessage, index.Name, d.Table, term.Column));
        var predicate = new List<Node>();
        if (IsLive(d, index)) predicate.Add(new IsNull(new ColumnRef(d.Soft!.Column)));
        predicate.AddRange(index.Filter.Select(f => f.ToNode()));
        return new(index.Name, d.Table, NodeList.Of(index.Terms.Select(t => t.ToNode())), index.Unique, predicate.Count == 0 ? null : new Where(NodeList.Of(predicate)));
    }

    private static ColumnDef Definition(ColumnDescriptor c) =>
        new(c.Column, new TypeRef(c.Type, c.Precision, c.Scale), c.Nullable, c.Key, c.Key || c.Nullable ? null : DefaultOf(c.Type));

    private static Node? DefaultOf(ColumnType type) => type switch
    {
        ColumnType.Text => Defaults.Text(""),
        ColumnType.Bool => Defaults.Flag(false),
        ColumnType.Int32 or ColumnType.Int64 or ColumnType.Double => Defaults.Number(0),
        ColumnType.Timestamp => Defaults.Now,
        ColumnType.Json => Defaults.EmptyDocument,
        _ => null,
    };

    private static Node Cell(ColumnDescriptor c, Param parameter) =>
        c.Type == ColumnType.Json ? new Cast(parameter, CastType.Json) : parameter;

    private static ColumnRef Of(string alias, string column) => new(column, new AliasRef(alias));

    private static NodeList ColumnList(IEnumerable<string> columns) => NodeList.Of(columns.Select(c => (Node)new ColumnRef(c)));

    private static IEnumerable<Node> KeyTerms(EntityDescriptor d, ColumnDescriptor key, Param value)
    {
        yield return new Compare(CompareOp.Equal, new ColumnRef(key.Column), value);
        if (d.Soft is { } soft) yield return new IsNull(new ColumnRef(soft.Column));
    }

    private static ColumnDescriptor RequireKey(EntityDescriptor d) =>
        d.SoleKey ?? throw (d.Key is null ? NoKey(d) : CompositeKey(d));

    private static InvalidOperationException NoKey(EntityDescriptor d) =>
        new(d.ClrType + " has no [Key] property; a statement by key needs one.");

    // ORML-61: a statement by key filters on ONE key column, so a descriptor with several [Key] flags is refused
    // instead of silently matching on the first (an UPDATE or DELETE would then touch every row sharing it).
    private static InvalidOperationException CompositeKey(EntityDescriptor d) =>
        new(d.ClrType + " has more than one [Key] property; a statement by key filters on a single key column, "
            + "so it is refused rather than matching on the first.");

    private static object? ValueOf(EntityDescriptor d, IReadOnlyList<object?> values, ColumnDescriptor column)
    {
        for (var i = 0; i < d.Columns.Count; i++)
            if (ReferenceEquals(d.Columns[i], column)) return values[i];
        throw new ArgumentException("not a column of " + d.ClrType, nameof(column));
    }

    private static void RequireWidth(EntityDescriptor d, IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != d.Columns.Count)
            throw new ArgumentException(d.ClrType + " has " + d.Columns.Count + " columns and the row carries " + values.Count + ".", nameof(values));
    }
}
