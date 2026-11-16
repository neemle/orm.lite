#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;

namespace Neemle.Orm.Lite;

    public sealed record SoftDeleteMeta(
        string Column,
        string[][]? LiveOnlyIndexes = null,
        string? TenantColumn = null,
        bool Platform = false,
        string[]? Unversioned = null,
        string? KeyColumn = null,
        string? LabelColumn = null,
        string[]? Owners = null,
        string? EntityToken = null,
        string[][]? PropertyColumns = null,
        string[]? PinnedColumns = null,
        string[]? StatusColumns = null)
    {
        public bool IsLiveOnlyTarget(IReadOnlyList<string> conflictColumns) =>
            LiveOnlyIndexes is not null
            && LiveOnlyIndexes.Any(index => index.Length == conflictColumns.Count && index.ToHashSet().SetEquals(conflictColumns));
    }

    public interface ISoftEntryVisitor<TResult>
    {
        TResult Visit<T>(SoftEntry<T> entry) where T : class;
    }

    public interface ISoftEntry
    {
        [EditorBrowsable(EditorBrowsableState.Never)] Type ClrType { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] string Table { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] string EntityToken { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] string KeyColumn { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] string SoftColumn { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] string? TenantColumn { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] bool Platform { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] string? LabelColumn { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] IReadOnlyList<string> Owners { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] IReadOnlyList<string> Unversioned { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] IReadOnlyList<string> Pinned { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] IReadOnlyList<string> StatusColumns { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] string? TypeColumnOfOwner(string ownerColumn);
        [EditorBrowsable(EditorBrowsableState.Never)] string? JsonNameOfColumn(string column);
        [EditorBrowsable(EditorBrowsableState.Never)] string? ColumnOfJsonName(string jsonName);
        [EditorBrowsable(EditorBrowsableState.Never)] TResult Accept<TResult>(ISoftEntryVisitor<TResult> visitor);
    }

    public sealed class SoftEntry<T> : ISoftEntry where T : class
    {
        private readonly Func<string, string> col;
        private readonly SoftDeleteMeta meta;
        private readonly Dictionary<string, string> jsonByColumn = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> columnByJson = new(StringComparer.Ordinal);

        public SoftEntry(string table, Func<string, string> col, SoftDeleteMeta meta)
        {
            Table = table;
            this.col = col;
            this.meta = meta;
            foreach (var pair in meta.PropertyColumns ?? [])
            {
                var json = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(pair[0]);
                jsonByColumn[pair[1]] = json;
                columnByJson[json] = pair[1];
            }
            var owners = new List<string>();
            if (!meta.Platform && meta.TenantColumn is { } tenant) owners.Add(tenant);
            foreach (var owner in meta.Owners ?? [])
                if (!owners.Contains(owner, StringComparer.Ordinal)) owners.Add(owner);
            Owners = owners;
            var pinned = new List<string>();
            var always = new List<string>();
            if (meta.KeyColumn is { } keyColumn) always.Add(keyColumn);
            always.Add(col(meta.Column));
            if (!meta.Platform && meta.TenantColumn is { } tenantColumn) always.Add(tenantColumn);
            foreach (var column in always.Concat(meta.PinnedColumns ?? []))
                if (!pinned.Contains(column, StringComparer.Ordinal)) pinned.Add(column);
            Pinned = pinned;
        }

        [EditorBrowsable(EditorBrowsableState.Never)] public Type ClrType => typeof(T);
        [EditorBrowsable(EditorBrowsableState.Never)] public string Table { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] public string EntityToken => meta.EntityToken ?? "";
        [EditorBrowsable(EditorBrowsableState.Never)] public string KeyColumn => meta.KeyColumn ?? "";
        [EditorBrowsable(EditorBrowsableState.Never)] public string SoftColumn => col(meta.Column);
        [EditorBrowsable(EditorBrowsableState.Never)] public string? TenantColumn => meta.TenantColumn;
        [EditorBrowsable(EditorBrowsableState.Never)] public bool Platform => meta.Platform;
        [EditorBrowsable(EditorBrowsableState.Never)] public string? LabelColumn => meta.LabelColumn;
        [EditorBrowsable(EditorBrowsableState.Never)] public IReadOnlyList<string> Owners { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] public IReadOnlyList<string> Unversioned => meta.Unversioned ?? [];
        [EditorBrowsable(EditorBrowsableState.Never)] public IReadOnlyList<string> Pinned { get; }
        [EditorBrowsable(EditorBrowsableState.Never)] public IReadOnlyList<string> StatusColumns => meta.StatusColumns ?? [];

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string? TypeColumnOfOwner(string ownerColumn)
        {
            if (!ownerColumn.EndsWith("_id", StringComparison.Ordinal)) return null;
            var candidate = ownerColumn[..^3] + "_type";
            return jsonByColumn.ContainsKey(candidate) ? candidate : null;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string? JsonNameOfColumn(string column) => jsonByColumn.TryGetValue(column, out var json) ? json : null;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string? ColumnOfJsonName(string jsonName) => columnByJson.TryGetValue(jsonName, out var column) ? column : null;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public TResult Accept<TResult>(ISoftEntryVisitor<TResult> visitor) => visitor.Visit(this);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Query<T> WriteQuery(OrmContext ctx, UnitOfWork? uow = null)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            var query = new Query<T>(ctx, static _ => throw new NotSupportedException("a write-only query maps no rows"));
            return uow is null ? query : query.On(uow);
        }
    }

    public static class OrmMeta
    {
        private static readonly ConcurrentDictionary<Type, (string Table, Func<string, string> Col, ImmutableArray<string> Columns)> Map = new();

        private static readonly ConcurrentDictionary<string, ImmutableArray<string>> ByTable = new();

        private static readonly ConcurrentDictionary<Type, SoftDeleteMeta> SoftDeletes = new();

        private static readonly ConcurrentDictionary<Type, ISoftEntry> SoftTyped = new();

        public static void Register<T>(string table, Func<string, string> col, ReadOnlySpan<string> columns, SoftDeleteMeta? softDelete = null) where T : class
        {
            var owned = ImmutableArray.Create(columns);
            if (softDelete is null)
            {
                SoftDeletes.TryRemove(typeof(T), out _);
                SoftTyped.TryRemove(typeof(T), out _);
            }
            else
            {
                SoftDeletes[typeof(T)] = softDelete;
                SoftTyped[typeof(T)] = new SoftEntry<T>(table, col, softDelete);
            }
            Map[typeof(T)] = (table, col, owned);
            ByTable[table] = owned;
        }

        public static (string Table, Func<string, string> Col, ImmutableArray<string> Columns) Get(Type t) =>
            Map.TryGetValue(t, out var m) ? m : throw new InvalidOperationException("No ORM metadata registered for " + t.FullName);

        public static bool TryGetSoftDelete(Type t, [MaybeNullWhen(false)] out SoftDeleteMeta softDelete) =>
            SoftDeletes.TryGetValue(t, out softDelete);

        public static IReadOnlyList<(Type Entity, string Table, SoftDeleteMeta Meta)> SoftEntries()
        {
            var entries = new List<(Type Entity, string Table, SoftDeleteMeta Meta)>();
            foreach (var soft in SoftDeletes)
                if (Map.TryGetValue(soft.Key, out var registered))
                    entries.Add((soft.Key, registered.Table, soft.Value));
            entries.Sort((a, b) =>
            {
                var byTable = string.CompareOrdinal(a.Table, b.Table);
                return byTable != 0 ? byTable : string.CompareOrdinal(a.Entity.FullName, b.Entity.FullName);
            });
            return entries;
        }

        public static IReadOnlyList<ISoftEntry> SoftTypedEntries()
        {
            var entries = SoftTyped.Values.ToList();
            entries.Sort((a, b) =>
            {
                var byTable = string.CompareOrdinal(a.Table, b.Table);
                return byTable != 0 ? byTable : string.CompareOrdinal(a.ClrType.FullName, b.ClrType.FullName);
            });
            return entries;
        }

        public static bool TryGetSoftEntry(Type t, [MaybeNullWhen(false)] out ISoftEntry entry) =>
            SoftTyped.TryGetValue(t, out entry);

        public static bool TryGetSoftEntryByToken(string token, [MaybeNullWhen(false)] out ISoftEntry entry)
        {
            entry = token.Length == 0 ? null : SoftTyped.Values.FirstOrDefault(e => string.Equals(e.EntityToken, token, StringComparison.Ordinal));
            return entry is not null;
        }

        public static ImmutableArray<string>? ColumnsForTable(string table) =>
            ByTable.TryGetValue(table, out var columns) ? columns : null;
    }
