#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed record ColumnDescriptor(string Property, string Column, ColumnType Type, bool Nullable, bool Key, int Precision = 0, int Scale = 0);

public sealed record IndexDescriptor(string Name, bool Unique, bool LiveOnly, IReadOnlyList<IndexTerm> Terms, IReadOnlyList<IndexCondition> Filter)
{
    public IReadOnlyList<string> Columns => [.. Terms.Select(t => t.Column)];

    public bool IsKey => Filter.Count == 0 && Terms.All(t => t.IsKey);

    public string Shape => Filter.Count == 0 && Terms.All(t => t == new IndexTerm(t.Column))
        ? ""
        : "|" + string.Join(",", Terms.Select(t => t.Identity)) + "|" + string.Join(" and ", Filter.Select(f => f.Identity));
}

public sealed record ManyToManyDescriptor(
    string Navigation,
    string JoinTable,
    string ThisKey,
    string OtherKey,
    string OtherTable,
    string OtherKeyColumn,
    string? OtherSoftColumn,
    bool Pure);

public sealed record SoftDeletion(
    string Property,
    string Column,
    bool Platform,
    string? TenantColumn,
    string? LabelColumn,
    IReadOnlyList<string> Unversioned,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> Pinned,
    IReadOnlyList<string> StatusColumns,
    string Token);

public sealed class EntityDescriptor
{
    public const int DecimalPrecision = 12;

    public const int DecimalScale = 4;

    private readonly List<ColumnDescriptor> _columns = [];

    private readonly List<IndexDescriptor> _indexes = [];

    private readonly List<ManyToManyDescriptor> _manyToMany = [];

    private EntityDescriptor(string clrType, string table, bool managed)
    {
        ClrType = clrType;
        Table = table;
        Managed = managed;
    }

    public string ClrType { get; }

    public string Table { get; }

    public bool Managed { get; }

    public bool IsView { get; private set; }

    public IReadOnlyList<ColumnDescriptor> Columns => _columns;

    public IReadOnlyList<IndexDescriptor> Indexes => _indexes;

    public IReadOnlyList<ManyToManyDescriptor> ManyToMany => _manyToMany;

    public SoftDeletion? Soft { get; private set; }

    public static EntityDescriptor Of(string clrType, string table, bool managed) =>
        new(clrType ?? throw new ArgumentNullException(nameof(clrType)), table ?? throw new ArgumentNullException(nameof(table)), managed);

    public const int MaxDecimalPrecision = 28;

    public EntityDescriptor View()
    {
        IsView = true;
        return this;
    }

    public EntityDescriptor Property(string property, string column, ColumnType type, bool nullable, bool key, int precision = 0, int scale = 0)
    {
        var (declaredPrecision, declaredScale) = type == ColumnType.Decimal ? DecimalShape(property, precision, scale) : (0, 0);
        _columns.Add(new ColumnDescriptor(property, column, type, nullable, key, declaredPrecision, declaredScale));
        return this;
    }

    private static (int Precision, int Scale) DecimalShape(string property, int precision, int scale)
    {
        if (precision == 0 && scale == 0) return (DecimalPrecision, DecimalScale);
        if (precision is < 1 or > MaxDecimalPrecision || scale < 0 || scale > precision)
            throw new ArgumentOutOfRangeException(nameof(precision), $"[Numeric({precision}, {scale})] on {property} is not a decimal shape: the precision is 1 to {MaxDecimalPrecision} and the scale is 0 to the precision.");
        return (precision, scale);
    }

    public EntityDescriptor Index(string name, bool unique, bool liveOnly, params string[] columns) =>
        Index(name, unique, liveOnly, columns.Select(c => new IndexTerm(c)).ToArray());

    public EntityDescriptor Index(string name, bool unique, bool liveOnly, params IndexTerm[] terms)
    {
        _indexes.Add(new IndexDescriptor(name, unique, liveOnly, terms, []));
        return this;
    }

    public EntityDescriptor Filter(params IndexCondition[] conditions)
    {
        var last = _indexes.Count - 1;
        _indexes[last] = _indexes[last] with { Filter = conditions };
        return this;
    }

    public EntityDescriptor Link(
        string navigation, string joinTable, string thisKey, string otherKey, string otherTable, string otherKeyColumn, string? otherSoftColumn, bool pure)
    {
        _manyToMany.Add(new ManyToManyDescriptor(navigation, joinTable, thisKey, otherKey, otherTable, otherKeyColumn, otherSoftColumn, pure));
        return this;
    }

    public EntityDescriptor SoftDelete(string property, string column, string token)
    {
        Soft = new SoftDeletion(property, column, true, null, null, [], [], [], [], token);
        return this;
    }

    public EntityDescriptor Tenant(string column) => With(s => s with { Platform = false, TenantColumn = column });

    public EntityDescriptor Label(string column) => With(s => s with { LabelColumn = column });

    public EntityDescriptor Unversioned(params string[] columns) => With(s => s with { Unversioned = columns });

    public EntityDescriptor Owners(params string[] columns) => With(s => s with { Owners = columns });

    public EntityDescriptor Pinned(params string[] columns) => With(s => s with { Pinned = columns });

    public EntityDescriptor StatusColumns(params string[] columns) => With(s => s with { StatusColumns = columns });

    private EntityDescriptor With(Func<SoftDeletion, SoftDeletion> change)
    {
        Soft = change(Soft ?? throw new InvalidOperationException("declare the soft deletion before describing it."));
        return this;
    }

    public ColumnDescriptor? Key => _columns.FirstOrDefault(c => c.Key);

    public ColumnDescriptor? SoleKey => _columns.Count(c => c.Key) == 1 ? _columns.First(c => c.Key) : null;

    public string? SoftColumn => Soft is null ? null : Soft.Column;

    public IReadOnlyList<ColumnDescriptor> SetColumns =>
        Key is null ? [] : _columns.Where(c => !c.Key && c.Property != Soft?.Property).ToList();

    public IReadOnlyList<string> ColumnNames => _columns.Select(c => c.Column).ToArray();

    public string ColumnOf(string property)
    {
        foreach (var column in _columns)
            if (string.Equals(column.Property, property, StringComparison.Ordinal)) return column.Column;
        throw new InvalidOperationException(UnmappedProperty(ClrType, property));
    }

    public SoftDeleteMeta? ToSoftMeta()
    {
        if (Soft is null) return null;
        var liveOnly = _indexes.Where(i => i.LiveOnly && i.IsKey).Select(i => i.Columns.ToArray()).ToArray();
        return new SoftDeleteMeta(
            Soft.Property,
            liveOnly.Length == 0 ? null : liveOnly,
            Soft.TenantColumn,
            Soft.Platform,
            Soft.Unversioned.Count == 0 ? null : Soft.Unversioned.ToArray(),
            SoleKey?.Column,
            Soft.LabelColumn,
            Soft.Owners.Count == 0 ? null : Soft.Owners.ToArray(),
            Soft.Token,
            _columns.Select(c => new[] { c.Property, c.Column }).ToArray(),
            Soft.Pinned.Count == 0 ? null : Soft.Pinned.ToArray(),
            Soft.StatusColumns.Count == 0 ? null : Soft.StatusColumns.ToArray());
    }

    public RowHistoryTable? HistoryTable()
    {
        if (Soft is null || SoleKey is not { } key) return null;
        if (!Soft.Platform && Soft.TenantColumn is null) return null;
        return new RowHistoryTable(Table, key.Column, Soft.Column, Soft.Platform ? "" : Soft.TenantColumn!, Soft.Unversioned.ToArray());
    }

    private static string UnmappedProperty(string clrType, string property) =>
        clrType + " has no COLUMN for the property `" + property + "`. This map answered the "
        + "property's own NAME for anything it did not recognise, and every caller of it splices "
        + "the answer into a statement as a quoted identifier -- so an unmapped property became a "
        + "column that does not exist and the statement failed at RUNTIME, from the database, "
        + "naming a column nobody wrote. That is reachable in ordinary code when a property stops being a column and stays a property: "
        + "`Where(w => w.CreatedVia == ...)` still compiles and can no longer be translated. "
        + "A moved field is filtered through the Db.MovedField* markers, which take its document "
        + "key; a property that is not persisted at all is not a predicate this builder can "
        + "express.";
}
