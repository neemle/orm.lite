#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;

namespace Neemle.Orm.Lite;

    public sealed class Query<T>
    {
        private readonly string _table;
        private readonly Func<DbDataReader, T> _map;
        private readonly Func<string, string> _col;
        private readonly ISqlDialect D;
        private readonly List<string> _where = new List<string>();
        private readonly SoftDeleteMeta? _softDelete;
        private bool _live;
        private readonly List<object?> _params = new List<object?>();
        private readonly Dictionary<string, string> _redact = new();
        private readonly OrmContext _db;
        private readonly RenderState _state;
        private readonly StatementExecutor _exec;
        private readonly PredicateTranslator _translator;
        private readonly SetClauses _setClauses;
        private readonly JsonClauses _json;
        private readonly WriteStatements _writes;
        private readonly SelectClauses _select;
        private readonly List<string> _aggregates = new List<string>();
        private readonly List<string> _projection = new List<string>();

        public Query(OrmContext db, Func<DbDataReader, T> map)
        {
            var meta = OrmMeta.Get(typeof(T));
            _db = db; D = db.Dialect; _table = meta.Table; _map = map; _col = meta.Col;
            _live = OrmMeta.TryGetSoftDelete(typeof(T), out _softDelete);
            _state = new RenderState(D, _table, _col, _params);
            _exec = new StatementExecutor(db.DataSource, _state);
            _select = new SelectClauses(D, _table);
            _translator = new PredicateTranslator(_state);
            _setClauses = new SetClauses(D);
            _json = new JsonClauses(_state, _translator, _setClauses);
            _writes = new WriteStatements(_state, _softDelete);
        }

        public Query<T> IncludingDeleted()
        {
            _live = false;
            return this;
        }

        private StringBuilder AppendWhere(StringBuilder sb, bool alwaysLive = false)
        {
            if (_live || (alwaysLive && _softDelete is not null))
            {
                sb.Append(" WHERE ").Append(SoftDeleteSql.LiveTerm(D, _col(_softDelete!.Column)));
                foreach (var term in _where) sb.Append(" AND ").Append(term);
            }
            else if (_where.Count > 0)
                sb.Append(" WHERE ").Append(string.Join(" AND ", _where));
            return sb;
        }

        private StringBuilder PhysicalDelete() => _writes.PhysicalDelete();

        private StringBuilder DeleteStatement() =>
            _softDelete is null
                ? PhysicalDelete()
                : new StringBuilder("UPDATE ").Append(D.Quote(_table))
                    .Append(" SET ").Append(SoftDeleteSql.SetClause(D, _col(_softDelete.Column)));

        public Query<T> On(DbConnection connection, DbTransaction? transaction = null)
        {
            _exec.On(connection, transaction);
            return this;
        }

        public Query<T> On(UnitOfWork unitOfWork)
        {
            if (unitOfWork is null) throw new ArgumentNullException(nameof(unitOfWork));
            return On(unitOfWork.Connection, unitOfWork.Transaction);
        }

        public (string Sql, List<object?> Parameters) RenderUpsert(
            Row<T> row,
            IReadOnlyList<Expression<Func<T, object?>>> conflictColumns,
            OnConflict action,
            IReadOnlyList<UpsertSet<T>>? sets = null,
            IReadOnlyList<Expression<Func<T, object?>>>? returning = null)
        {
            var resolved = RowResolver.ResolveRow(_translator, row);
            return _writes.RenderUpsert(
                resolved, RowResolver.ResolveColumns(_translator, conflictColumns), action,
                RowResolver.ResolveSets(_translator, resolved, sets), ResolveRegistered(returning));
        }

        public async Task<List<object?[]>> UpsertAsync(
            Row<T> row,
            IReadOnlyList<Expression<Func<T, object?>>> conflictColumns,
            OnConflict action,
            IReadOnlyList<UpsertSet<T>>? sets = null,
            IReadOnlyList<Expression<Func<T, object?>>>? returning = null)
        {
            var (text, ps) = RenderUpsert(row, conflictColumns, action, sets, returning);
            return await _exec.UpsertAsync(
                text, ps, returning?.Count ?? 0, _writes.RefusesSoftDeletedConflict(action, sets?.Count ?? 0));
        }

        public string RenderUpdateReturning(IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            RenderUpdateReturningCore(ResolveRegistered(returning));

        private string RenderUpdateReturningCore(IReadOnlyList<string> returning)
        {
            if (!D.SupportsReturning)
                throw new NotSupportedException(
                    $"{D.GetType().Name} cannot return columns from a write; select the affected ids first, "
                    + "inside the same transaction.");
            if (_setClauses.Count == 0)
                throw new InvalidOperationException("UpdateReturningAsync needs at least one Set().");
            if (_where.Count == 0)
                throw new InvalidOperationException(
                    "UpdateReturningAsync requires a Where (refusing an unfiltered UPDATE).");
            var sb = AppendWhere(
                    new StringBuilder("UPDATE ").Append(D.Quote(_table)).Append(" SET ").Append(_setClauses.Joined))
                .Append(D.ReturningClause(returning));
            return sb.ToString();
        }

        public async Task<List<object?[]>> UpdateReturningAsync(
            IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            await _exec.ReturningAsync(RenderUpdateReturningCore(ResolveRegistered(returning)), returning.Count);

        public string RenderDeleteReturning(IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            RenderDeleteReturningCore(ResolveRegistered(returning));

        private string RenderDeleteReturningCore(IReadOnlyList<string> returning)
        {
            if (!D.SupportsReturning)
                throw new NotSupportedException(
                    $"{D.GetType().Name} cannot return columns from a write; select the affected rows first, "
                    + "inside the same transaction.");
            if (_where.Count == 0)
                throw new InvalidOperationException(
                    "DeleteReturningAsync requires a Where (refusing an unfiltered DELETE).");
            var sb = AppendWhere(DeleteStatement(), alwaysLive: true)
                .Append(D.ReturningClause(returning));
            return sb.ToString();
        }

        public async Task<List<object?[]>> DeleteReturningAsync(
            IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            await _exec.ReturningAsync(RenderDeleteReturningCore(ResolveRegistered(returning)), returning.Count);

        public Query<T> Where(Expression<Func<T, bool>> predicate)
        {
            _where.Add(_translator.Predicate(predicate.Parameters[0], predicate.Body));
            return this;
        }

        public Query<T> WhereExists<TOther>(Expression<Func<T, TOther, bool>> predicate)
        {
            var outer = predicate.Parameters[0];
            var inner = predicate.Parameters[1];
            var meta = OrmMeta.Get(typeof(TOther));
            var alias = _state.NextAlias();
            _state.Scope[outer] = (D.Quote(_table), _col);
            _state.Scope[inner] = (alias, meta.Col);
            _where.Add("EXISTS (SELECT 1 FROM " + D.Quote(meta.Table) + " " + alias + " WHERE "
                + _translator.Translate(predicate.Body)
                + _state.LiveTermOf(typeof(TOther), alias, meta.Col) + ")");
            _state.Scope.Remove(outer);
            _state.Scope.Remove(inner);
            return this;
        }

        public Query<T> Redact(Expression<Func<T, object?>> column)
        {
            _redact[_translator.RawColumnOf(column.Body)] = "NULL";
            return this;
        }

        public Query<T> RedactWhen(Expression<Func<T, object?>> column, Expression<Func<T, bool>> when)
        {
            var raw = _translator.RawColumnOf(column.Body);
            var cond = _translator.Predicate(when.Parameters[0], when.Body);
            _redact[raw] = "CASE WHEN " + cond + " THEN NULL ELSE " + D.Quote(raw) + " END";
            return this;
        }

        public Query<T> OrderBy(Expression<Func<T, object?>> selector)
        { _select.ReplaceOrder(_translator.ColumnOf(selector.Body) + " ASC"); return this; }

        public Query<T> OrderByDescending(Expression<Func<T, object?>> selector)
        { _select.ReplaceOrder(_translator.ColumnOf(selector.Body) + " DESC"); return this; }

        public Query<T> ThenBy(Expression<Func<T, object?>> selector)
        { _select.AddOrder(_translator.ColumnOf(selector.Body) + " ASC"); return this; }

        public Query<T> ThenByDescending(Expression<Func<T, object?>> selector)
        { _select.AddOrder(_translator.ColumnOf(selector.Body) + " DESC"); return this; }
        public Query<T> Take(int n) { _select.Limit = n; return this; }
        public Query<T> Skip(int n) { _select.Offset = n; return this; }

        public Query<T> ForUpdate()
        {
            if (!D.SupportsRowLock)
                throw new NotSupportedException(
                    $"{D.GetType().Name} has no row lock; a caller that needs one cannot be served by an "
                    + "unlocked read.");
            _select.LockRows();
            return this;
        }

        public Query<T> ForNoKeyUpdate()
        {
            if (!D.SupportsKeyPreservingRowLock)
                throw new NotSupportedException(
                    $"{D.GetType().Name} has no key-preserving row lock; a caller that needs to serialise writers "
                    + "of a row WITHOUT blocking a foreign key that references it cannot be served by an unlocked "
                    + "read, and must not be silently handed the stronger lock.");
            _select.LockRowsKeyPreserving();
            return this;
        }

        public Query<T> OrderByPredicate(Expression<Func<T, bool>> selector)
        { _select.ReplaceOrder(PredicateTerm(selector) + " ASC"); return this; }

        public Query<T> ThenByPredicate(Expression<Func<T, bool>> selector)
        { _select.AddOrder(PredicateTerm(selector) + " ASC"); return this; }

        public Query<T> OrderByMovedFieldNumberOrEpochMillis(
            Expression<Func<T, object?>> document, MovedFieldKey key, Expression<Func<T, object?>> timestamp)
        {
            _select.ReplaceOrder(_json.MovedFieldNumberOrEpochMillisOrderTerm(document, key, timestamp));
            return this;
        }

        public Query<T> OrderByJsonKey(
            Expression<Func<T, object?>> column, JsonKey key, bool descending, bool nullsFirst)
        { _select.ReplaceOrder(_json.OrderTerm(column, key, descending, nullsFirst)); return this; }

        public Query<T> ThenByJsonKey(
            Expression<Func<T, object?>> column, JsonKey key, bool descending, bool nullsFirst)
        { _select.AddOrder(_json.OrderTerm(column, key, descending, nullsFirst)); return this; }

        public Query<T> OrderByJsonKeyOf<TOther>(
            Expression<Func<T, TOther, bool>> correlation,
            Expression<Func<TOther, object?>> column,
            JsonKeyOrdering ordering)
        {
            _select.ReplaceOrder(_json.OrderTermOf(typeof(TOther), correlation, column, ordering));
            return this;
        }

        public Query<T> ThenByJsonKeyOf<TOther>(
            Expression<Func<T, TOther, bool>> correlation,
            Expression<Func<TOther, object?>> column,
            JsonKeyOrdering ordering)
        { _select.AddOrder(_json.OrderTermOf(typeof(TOther), correlation, column, ordering)); return this; }

        private string PredicateTerm(Expression<Func<T, bool>> selector) =>
            "(" + _translator.Predicate(selector.Parameters[0], selector.Body) + ")";

        public async Task<List<T>> ToListAsync() =>
            await _exec.RowsAsync(RenderSelect(), _map);

        public async Task<List<TOut>> ProjectAsync<TOut>(
            IReadOnlyList<Expression<Func<T, object?>>> columns,
            Func<DbDataReader, TOut> map)
        {
            if (columns.Count == 0)
                throw new InvalidOperationException("ProjectAsync needs at least one column.");
            return await _exec.RowsAsync(RenderProject(columns), map);
        }

        public string RenderProject(IReadOnlyList<Expression<Func<T, object?>>> columns)
        {
            if (columns.Count == 0)
                throw new InvalidOperationException("ProjectAsync needs at least one column.");
            return _select.Render(string.Join(", ", columns.Select(c => _translator.ColumnOf(c.Body))), WhereText());
        }

        public Query<T> ProjectColumn(Expression<Func<T, object?>> column)
        { _projection.Add(_translator.ColumnOf(column.Body)); return this; }

        public Query<T> ProjectPredicate(Expression<Func<T, bool>> predicate)
        { _projection.Add(PredicateTerm(predicate)); return this; }

        public string RenderProjection()
        {
            if (_projection.Count == 0)
                throw new InvalidOperationException(
                    "ProjectionAsync needs at least one ProjectColumn/ProjectPredicate term.");
            return _select.Render(string.Join(", ", _projection), WhereText());
        }

        public async Task<List<TOut>> ProjectionAsync<TOut>(Func<DbDataReader, TOut> map) =>
            await _exec.RowsAsync(RenderProjection(), map);

        public async Task<T?> FirstOrDefaultAsync()
        { _select.Limit = 1; var l = await ToListAsync(); return l.Count > 0 ? l[0] : default; }

        public async Task<long> CountAsync()
        {
            var sb = new StringBuilder("SELECT count(*) FROM ").Append(D.Quote(_table));
            AppendWhere(sb);
            return (long)(await _exec.ScalarAsync(sb.ToString()) ?? 0L);
        }

        public async Task<long> SumAsync(Expression<Func<T, object?>> column)
        {
            var sb = new StringBuilder("SELECT coalesce(sum(").Append(_translator.ColumnOf(column.Body))
                .Append("), 0) FROM ").Append(D.Quote(_table));
            AppendWhere(sb);
            return System.Convert.ToInt64(await _exec.ScalarAsync(sb.ToString()) ?? 0L);
        }

        public async Task<TCol> ScalarAsync<TCol>(Expression<Func<T, object?>> column)
        {
            var sb = new StringBuilder("SELECT ").Append(_translator.ColumnOf(column.Body))
                .Append(" FROM ").Append(D.Quote(_table));
            AppendWhere(sb);
            sb.Append(D.Page(1, null));
            var v = await _exec.ScalarAsync(sb.ToString());
            return v is null || v is DBNull ? default! : (TCol)v;
        }

        public async Task<bool> AnyAsync() => await CountAsync() > 0;

        public Query<T> AggregateCount()
        { _aggregates.Add(D.CountAllExpression); return this; }

        public Query<T> AggregateCountDistinct(Expression<Func<T, object?>> column)
        { _aggregates.Add(D.CountDistinctExpression(_translator.ColumnOf(column.Body))); return this; }

        public Query<T> AggregateSum(Expression<Func<T, object?>> column)
        { _aggregates.Add(D.SumCoalescedExpression(_translator.ColumnOf(column.Body))); return this; }

        public Query<T> AggregateMin(Expression<Func<T, object?>> column)
        { _aggregates.Add(D.MinExpression(_translator.ColumnOf(column.Body))); return this; }

        public Query<T> AggregateCountWhere(Expression<Func<T, bool>> filter)
        {
            if (!D.SupportsAggregateFilter)
                throw new NotSupportedException(
                    $"{D.GetType().Name} has no FILTER aggregate; a second statement would read a different snapshot.");
            _aggregates.Add(D.CountFilterExpression(PredicateTerm(filter)));
            return this;
        }

        public string RenderAggregates()
        {
            if (_aggregates.Count == 0)
                throw new InvalidOperationException("AggregatesAsync needs at least one aggregate term.");
            var sb = new StringBuilder("SELECT ").Append(string.Join(", ", _aggregates))
                .Append(" FROM ").Append(D.Quote(_table));
            AppendWhere(sb);
            return sb.ToString();
        }

        public async Task<object?[]> AggregatesAsync() =>
            await _exec.AggregateRowAsync(RenderAggregates(), _aggregates.Count);

        public Query<T> WhereIn<TOther>(
            Expression<Func<T, object?>> column,
            Func<Query<TOther>, Query<TOther>> build,
            Expression<Func<TOther, object?>> selected)
        {
            var sub = new Query<TOther>(
                _db,
                _ => throw new NotSupportedException("A WhereIn subquery projects columns and never maps rows."));
            sub._state.ParamBase = _state.ParamBase + _params.Count;
            var built = build(sub);
            if (!ReferenceEquals(built, sub))
                throw new InvalidOperationException(
                    "A WhereIn builder must return the query instance it was given.");
            var text = sub.RenderProject(new[] { selected });
            _where.Add(_translator.ColumnOf(column.Body) + " IN (" + text + ")");
            _params.AddRange(sub._params);
            return this;
        }

        public (string Sql, List<object?> Parameters) RenderInsertMany(IReadOnlyList<Row<T>> rows) =>
            _writes.RenderInsertMany(RowResolver.ResolveInsertRows(_translator, rows));

        public async Task<int> InsertManyAsync(IReadOnlyList<Row<T>> rows)
        {
            var chunks = InsertManyChunks.Split(RowResolver.ResolveInsertRows(_translator, rows), D.MaxBindParameters);
            var statements = chunks.Select(_writes.RenderInsertMany).ToList();
            return await _exec.InsertManyAsync(statements);
        }

        public Query<T> Set(Expression<Func<T, object?>> column, object? value)
        {
            var target = _translator.ColumnOf(column.Body);
            _setClauses.Add(target, target + " = " + _state.ValueSlot(value, _state.Slot(_params.Count)));
            _params.Add(value);
            return this;
        }

        public Query<T> SetNow(Expression<Func<T, object?>> column)
        {
            var target = _translator.ColumnOf(column.Body);
            _setClauses.Add(target, target + " = " + D.NowExpression);
            return this;
        }

        public Query<T> SetCoalesce(Expression<Func<T, object?>> column, object? value)
        {
            var target = _translator.ColumnOf(column.Body);
            _setClauses.Add(target, D.CoalesceAssignment(target, _state.ValueSlot(value, _state.Slot(_params.Count))));
            _params.Add(value);
            return this;
        }

        public Query<T> SetNowPlusMinutes(Expression<Func<T, object?>> column, int minutes)
        {
            if (!D.SupportsIntervalArithmetic)
                throw new NotSupportedException(
                    $"{D.GetType().Name} has no interval arithmetic; computing the timestamp in the caller "
                    + "would move the lease clock off the server that compares it.");
            var target = _translator.ColumnOf(column.Body);
            _setClauses.Add(target, target + " = " + D.NowPlusMinutesExpression(_state.Slot(_params.Count)));
            _params.Add(minutes);
            return this;
        }

        public Query<T> SetIncrement(Expression<Func<T, object?>> column, int amount)
        {
            var target = _translator.ColumnOf(column.Body);
            _setClauses.Add(target, D.IncrementAssignment(target, _state.Slot(_params.Count)));
            _params.Add(amount);
            return this;
        }

        public Query<T> SetColumn(Expression<Func<T, object?>> column, Expression<Func<T, object?>> source)
        {
            var target = _translator.ColumnOf(column.Body);
            _setClauses.Add(target, target + " = " + _translator.ColumnOf(source.Body));
            return this;
        }

        public Query<T> SetPredicate(Expression<Func<T, object?>> column, Expression<Func<T, bool>> value)
        {
            var target = _translator.ColumnOf(column.Body);
            var rendered = _translator.Predicate(value.Parameters[0], value.Body);
            _setClauses.Add(target, target + " = (" + rendered + ")");
            return this;
        }

        public Query<T> SetJsonMerge(Expression<Func<T, object?>> document, Jsonb patch)
        {
            _json.Merge(document, patch);
            return this;
        }

        public Query<T> SetJsonRemoveThenMerge(
            Expression<Func<T, object?>> document,
            IReadOnlyList<MovedFieldKey> clearedKeys,
            IReadOnlyList<MovedFieldKey> setKeys,
            Jsonb patch)
        {
            _json.RemoveThenMerge(document, clearedKeys, setKeys, patch);
            return this;
        }

        public Query<T> SetJsonMemberIncrement(
            Expression<Func<T, object?>> document, MovedFieldKey key, int amount)
        {
            _json.MemberIncrement(document, key, amount);
            return this;
        }

        public string RenderUpdate()
        {
            if (_setClauses.Count == 0)
                throw new InvalidOperationException("UpdateAsync requires at least one Set/SetNow.");
            if (_where.Count == 0)
                throw new InvalidOperationException("UpdateAsync requires a Where (refusing an unfiltered UPDATE).");
            return AppendWhere(new StringBuilder("UPDATE ").Append(D.Quote(_table))
                .Append(" SET ").Append(_setClauses.Joined))
                .ToString();
        }

        public async Task<int> UpdateAsync() =>
            await _exec.NonQueryAsync(RenderUpdate());

        public string RenderDelete()
        {
            if (_where.Count == 0)
                throw new InvalidOperationException("DeleteAsync requires a Where (refusing an unfiltered DELETE).");
            return AppendWhere(DeleteStatement(), alwaysLive: true)
                .ToString();
        }

        public async Task<int> DeleteAsync() =>
            await _exec.NonQueryAsync(RenderDelete());

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string RenderPurge()
        {
            if (_where.Count == 0)
                throw new InvalidOperationException("PurgeAsync requires a Where (refusing an unfiltered DELETE).");
            return AppendWhere(IncludingDeleted().PhysicalDelete()).ToString();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task<int> PurgeAsync() =>
            await _exec.NonQueryAsync(RenderPurge());

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string RenderPurge(IReadOnlyCollection<Guid> ids, Guid? tenantId) =>
            _writes.RenderPurgeByKey(ids, tenantId);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task<int> PurgeAsync(IReadOnlyCollection<Guid> ids, Guid? tenantId)
        {
            var sql = RenderPurge(ids, tenantId);
            _params.Add(ids.ToArray());
            if (tenantId is { } tenant) _params.Add(tenant);
            return await _exec.NonQueryAsync(sql);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string RenderRestore(IReadOnlyCollection<Guid> ids, Guid? tenantId) =>
            _writes.RenderRestore(ids, tenantId);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task<int> RestoreAsync(IReadOnlyCollection<Guid> ids, Guid? tenantId)
        {
            var sql = RenderRestore(ids, tenantId);
            _params.Add(ids.ToArray());
            if (tenantId is { } tenant) _params.Add(tenant);
            return await _exec.NonQueryAsync(sql);
        }

        public IReadOnlyList<object?> BoundParameters => _params;

        public IReadOnlyList<string> WhereClauses => _where;

        public void Bind(DbCommand cmd) => _exec.Bind(cmd);

        public string RenderSelect() => _select.Render(SelectedColumns(), WhereText());

        private string SelectedColumns() =>
            _redact.Count == 0
                ? "*"
                : string.Join(", ", OrmMeta.Get(typeof(T)).Columns.Select(c =>
                    _redact.TryGetValue(c, out var expr) ? expr + " AS " + D.Quote(c) : D.Quote(c)));

        private string WhereText() => AppendWhere(new StringBuilder()).ToString();

        private IReadOnlyList<string> ResolveRegistered(IReadOnlyList<Expression<Func<T, object?>>>? columns) =>
            RowResolver.ResolveRegistered(_translator, columns);
    }
