#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Threading;
using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

    public sealed class Query<T>
    {
        private readonly string _table;
        private readonly Func<DbDataReader, T> _map;
        private readonly Func<string, string> _col;
        private readonly List<Node> _where = new List<Node>();
        private readonly SoftDeleteMeta? _softDelete;
        private bool _live;
        private readonly List<object?> _params = new List<object?>();
        private readonly OrmContext _db;
        private readonly RenderState _state;
        private readonly StatementExecutor _exec;
        private UnitOfWork? _streamUnit;
        private readonly PredicateTranslator _translator;
        private readonly SetClauses _setClauses;
        private readonly SetTerms _set;
        private readonly JsonClauses _json;
        private readonly WriteStatements _writes;
        private readonly WriteCompiler _compiler;
        private readonly RowWrites<T> _rows;
        private readonly SelectClauses _select;
        private readonly RowLockClauses _locks;
        private readonly AggregateClauses _aggregates;
        private readonly List<Node> _projection = new List<Node>();

        public Query(OrmContext db, Func<DbDataReader, T> map)
        {
            var meta = OrmMeta.Get(typeof(T));
            _db = db; _table = meta.Table; _map = map; _col = meta.Col;
            _live = OrmMeta.TryGetSoftDelete(typeof(T), out _softDelete);
            _state = new RenderState(db.Provider, _table, _col, _params);
            _exec = new StatementExecutor(db.DataSource, _state);
            _select = new SelectClauses(_table);
            _translator = new PredicateTranslator(_state);
            _setClauses = new SetClauses(_state.Quote);
            _set = new SetTerms(_state, _translator, _setClauses);
            _json = new JsonClauses(_state, _translator, _setClauses);
            _writes = new WriteStatements(_state, _softDelete);
            _compiler = new WriteCompiler(_state, _select, _softDelete, OrmMeta.IsView(typeof(T)));
            _rows = new RowWrites<T>(_translator, _writes);
            _locks = new RowLockClauses(_state, _select);
            _aggregates = new AggregateClauses(_state, _translator);
        }

        public Query<T> IncludingDeleted()
        {
            _live = false;
            return this;
        }

        private Node LiveTerm() =>
            new IsNull(new ColumnRef(_col(_softDelete!.Column), _state.Qualified ? new TableRef(_table) : null));

        private enum LiveScope { PerQuery, Always, Never }

        private Where? WhereNode(LiveScope scope = LiveScope.PerQuery)
        {
            var terms = new List<Node>();
            var live = scope switch
            {
                LiveScope.Always => _live || _softDelete is not null,
                LiveScope.Never => false,
                _ => _live,
            };
            if (live) terms.Add(LiveTerm());
            terms.AddRange(_where);
            return terms.Count == 0 ? null : new Where(NodeList.Of(terms));
        }

        public Query<T> On(DbConnection connection, DbTransaction? transaction = null)
        {
            _exec.On(connection, transaction);
            _streamUnit = null;
            return this;
        }

        public Query<T> On(UnitOfWork unitOfWork)
        {
            if (unitOfWork is null) throw new ArgumentNullException(nameof(unitOfWork));
            On(unitOfWork.Connection, unitOfWork.Transaction);
            _streamUnit = unitOfWork;
            return this;
        }

        public Statement CompileUpsert(
            Row<T> row,
            IReadOnlyList<Expression<Func<T, object?>>> conflictColumns,
            OnConflict action,
            IReadOnlyList<UpsertSet<T>>? sets = null,
            IReadOnlyList<Expression<Func<T, object?>>>? returning = null) =>
            CompileWrite(_rows.Upsert(row, conflictColumns, action, sets, returning).Tree);

        public (string Sql, List<object?> Parameters) RenderUpsert(
            Row<T> row,
            IReadOnlyList<Expression<Func<T, object?>>> conflictColumns,
            OnConflict action,
            IReadOnlyList<UpsertSet<T>>? sets = null,
            IReadOnlyList<Expression<Func<T, object?>>>? returning = null)
        {
            var built = _rows.Upsert(row, conflictColumns, action, sets, returning);
            return (CompileWrite(built.Tree).Text, built.Bound);
        }

        [RequiresCapability(Capability.Upsert)]
        public async Task<List<object?[]>> UpsertAsync(
            Row<T> row,
            IReadOnlyList<Expression<Func<T, object?>>> conflictColumns,
            OnConflict action,
            IReadOnlyList<UpsertSet<T>>? sets = null,
            IReadOnlyList<Expression<Func<T, object?>>>? returning = null)
        {
            var statement = CompileUpsert(row, conflictColumns, action, sets, returning);
            return await _exec.UpsertAsync(
                statement, returning?.Count ?? 0, _writes.RefusesSoftDeletedConflict(action, sets?.Count ?? 0));
        }

        public Statement CompileUpdateReturning(IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            CompileUpdateReturningCore(_rows.Registered(returning));

        public string RenderUpdateReturning(IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            CompileUpdateReturning(returning).Text;

        private Statement CompileUpdateReturningCore(IReadOnlyList<string> returning)
        {
            _compiler.RequireReturning("ids");
            if (_setClauses.Count == 0)
                throw new InvalidOperationException("UpdateReturningAsync needs at least one Set().");
            if (_where.Count == 0)
                throw new InvalidOperationException(
                    "UpdateReturningAsync requires a Where (refusing an unfiltered UPDATE).");
            return CompileQueryWrite("UpdateReturningAsync", new Update(
                new TableRef(_table), _setClauses.Assignments, WhereNode(), _writes.ReturningOf(returning)));
        }

        [RequiresCapability(Capability.Returning)]
        public async Task<List<object?[]>> UpdateReturningAsync(
            IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            await _exec.ReturningAsync(CompileUpdateReturningCore(_rows.Registered(returning)), returning.Count);

        public Statement CompileDeleteReturning(IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            CompileDeleteReturningCore(_rows.Registered(returning));

        public string RenderDeleteReturning(IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            CompileDeleteReturning(returning).Text;

        private Statement CompileDeleteReturningCore(IReadOnlyList<string> returning)
        {
            _compiler.RequireReturning("rows");
            if (_where.Count == 0)
                throw new InvalidOperationException(
                    "DeleteReturningAsync requires a Where (refusing an unfiltered DELETE).");
            return CompileQueryWrite(
                "DeleteReturningAsync",
                _compiler.DeleteTree(WhereNode(LiveScope.Always), _writes.ReturningOf(returning)));
        }

        [RequiresCapability(Capability.Returning)]
        public async Task<List<object?[]>> DeleteReturningAsync(
            IReadOnlyList<Expression<Func<T, object?>>> returning) =>
            await _exec.ReturningAsync(CompileDeleteReturningCore(_rows.Registered(returning)), returning.Count);

        public Query<T> Where(Expression<Func<T, bool>> predicate)
        {
            _where.Add(_translator.Predicate(predicate.Parameters[0], predicate.Body));
            return this;
        }

        public Query<T> WhereExists<TOther>(Expression<Func<T, TOther, bool>> predicate)
        {
            _where.Add(CorrelatedExists.Of(_state, _translator, typeof(TOther), predicate));
            return this;
        }

        public Query<T> Redact(Expression<Func<T, object?>> column)
        { Redaction.Hide(_state, _translator, column.Body); return this; }

        public Query<T> RedactWhen(Expression<Func<T, object?>> column, Expression<Func<T, bool>> when)
        { Redaction.HideWhen(_state, _translator, column.Body, when); return this; }

        public Query<T> OrderBy(Expression<Func<T, object?>> selector)
        { _select.ReplaceOrder(PlainOrder.Of(_translator.OrderColumnOf(selector.Body), false)); return this; }

        public Query<T> OrderByDescending(Expression<Func<T, object?>> selector)
        { _select.ReplaceOrder(PlainOrder.Of(_translator.OrderColumnOf(selector.Body), true)); return this; }

        public Query<T> ThenBy(Expression<Func<T, object?>> selector)
        { _select.AddOrder(PlainOrder.Of(_translator.OrderColumnOf(selector.Body), false)); return this; }

        public Query<T> ThenByDescending(Expression<Func<T, object?>> selector)
        { _select.AddOrder(PlainOrder.Of(_translator.OrderColumnOf(selector.Body), true)); return this; }
        public Query<T> Take(int n) { _select.Limit = n; return this; }
        public Query<T> Skip(int n) { _select.Offset = n; return this; }

        [RequiresCapability(Capability.RowLock)]
        public Query<T> ForUpdate()
        {
            _locks.ForUpdate();
            return this;
        }

        [RequiresCapability(Capability.RowLock)]
        [RequiresCapability(Capability.KeyPreservingRowLock)]
        public Query<T> ForNoKeyUpdate()
        {
            _locks.ForNoKeyUpdate();
            return this;
        }

        public Query<T> OrderByPredicate(Expression<Func<T, bool>> selector)
        { _select.ReplaceOrder(PlainOrder.Of(PredicateTerm(selector), false)); return this; }

        public Query<T> ThenByPredicate(Expression<Func<T, bool>> selector)
        { _select.AddOrder(PlainOrder.Of(PredicateTerm(selector), false)); return this; }

        [RequiresCapability(Capability.JsonDocument)]
        public Query<T> OrderByMovedFieldNumberOrEpochMillis(
            Expression<Func<T, object?>> document, MovedFieldKey key, Expression<Func<T, object?>> timestamp)
        {
            _select.ReplaceOrder(_json.MovedFieldNumberOrEpochMillisOrderTerm(document, key, timestamp));
            return this;
        }

        [RequiresCapability(Capability.JsonDocument)]
        public Query<T> OrderByJsonKey(
            Expression<Func<T, object?>> column, JsonKey key, bool descending, bool nullsFirst)
        { _select.ReplaceOrder(_json.KeyTerm(column, key, descending, nullsFirst)); return this; }

        [RequiresCapability(Capability.JsonDocument)]
        public Query<T> ThenByJsonKey(
            Expression<Func<T, object?>> column, JsonKey key, bool descending, bool nullsFirst)
        { _select.AddOrder(_json.KeyTerm(column, key, descending, nullsFirst)); return this; }

        [RequiresCapability(Capability.JsonDocument)]
        public Query<T> OrderByJsonKeyOf<TOther>(
            Expression<Func<T, TOther, bool>> correlation,
            Expression<Func<TOther, object?>> column,
            JsonKeyOrdering ordering)
        {
            _select.ReplaceOrder(_json.KeyOfTerm(typeof(TOther), correlation, column, ordering));
            return this;
        }

        [RequiresCapability(Capability.JsonDocument)]
        public Query<T> ThenByJsonKeyOf<TOther>(
            Expression<Func<T, TOther, bool>> correlation,
            Expression<Func<TOther, object?>> column,
            JsonKeyOrdering ordering)
        { _select.AddOrder(_json.KeyOfTerm(typeof(TOther), correlation, column, ordering)); return this; }

        private Node PredicateTerm(Expression<Func<T, bool>> selector) =>
            new Group(_translator.Predicate(selector.Parameters[0], selector.Body));

        public QueryStream<T> StreamAsync(CancellationToken ct = default) =>
            new(QueryStreamCapture.Entities(this), ct);

        public QueryStream<TOut> StreamProjectAsync<TOut>(IReadOnlyList<Expression<Func<T, object?>>> columns,
            Func<DbDataReader, TOut> map, CancellationToken ct = default) =>
            new(QueryStreamCapture.Project(this, columns, map), ct);

        public QueryStream<TOut> StreamProjectionAsync<TOut>(Func<DbDataReader, TOut> map,
            CancellationToken ct = default) => new(QueryStreamCapture.Projection(this, map), ct);

        public QueryStream<TCol> StreamColumnAsync<TCol>(Expression<Func<T, object?>> column,
            CancellationToken ct = default) => new(QueryStreamCapture.Column<T, TCol>(this, column), ct);

        internal Func<DbDataReader, T> StreamMap => _map;
        internal StreamBinding StreamBinding => new(_db.DataSource, _db.Provider);
        internal StreamBorrowed? StreamBorrowed => _exec.StreamScope(_streamUnit);
        internal RowLock? StreamRowLock => _select.Build([], null).Lock;

        public Task<List<T>> ToListAsync() => QueryPolicyReads<T>.RowsAsync(this, _exec);

        public async Task<List<TOut>> ProjectAsync<TOut>(
            IReadOnlyList<Expression<Func<T, object?>>> columns,
            Func<DbDataReader, TOut> map) =>
            await _exec.RowsAsync(CompileProject(columns),
                GraphSaveOrigins.Wrap(OrphanReadOrigins.Wrap(map, true), true));

        public Statement CompileProject(IReadOnlyList<Expression<Func<T, object?>>> columns) =>
            CompileTree(BuildProject(columns));

        public string RenderProject(IReadOnlyList<Expression<Func<T, object?>>> columns) =>
            CompileProject(columns).Text;

        internal Select BuildProject(IReadOnlyList<Expression<Func<T, object?>>> columns)
        {
            if (columns.Count == 0)
                throw new InvalidOperationException("ProjectAsync needs at least one column.");
            return _select.Build(columns.Select(c => _translator.MaskedColumnOf(c.Body)).ToList(), WhereNode());
        }

        public Query<T> ProjectColumn(Expression<Func<T, object?>> column)
        { _projection.Add(_translator.MaskedColumnOf(column.Body)); return this; }

        public Query<T> ProjectPredicate(Expression<Func<T, bool>> predicate)
        { _projection.Add(PredicateTerm(predicate)); return this; }

        public Statement CompileProjection() => CompileTree(BuildProjection());

        public string RenderProjection() => CompileProjection().Text;

        internal Select BuildProjection()
        {
            if (_projection.Count == 0)
                throw new InvalidOperationException(
                    "ProjectionAsync needs at least one ProjectColumn/ProjectPredicate term.");
            return _select.Build(_projection, WhereNode());
        }

        public async Task<List<TOut>> ProjectionAsync<TOut>(Func<DbDataReader, TOut> map) =>
            await _exec.RowsAsync(CompileProjection(),
                GraphSaveOrigins.Wrap(OrphanReadOrigins.Wrap(map, true), true));

        public Task<T?> FirstOrDefaultAsync() => QueryPolicyReads<T>.FirstAsync(this, _exec);

        public async Task<long> CountAsync()
        {
            RefuseClauses("CountAsync", ClauseRule.Aggregate);
            return await CountRowsAsync();
        }

        private async Task<long> CountRowsAsync()
        {
            var tree = new Select([new Call(Fn.Count, [new Star()])], new TableRef(_table), WhereNode());
            return (long)(await _exec.ScalarAsync(CompileTree(tree)) ?? 0L);
        }

        public async Task<long> SumAsync(Expression<Func<T, object?>> column)
        {
            LongSum.Require(column.Body);
            RefuseClauses("SumAsync", ClauseRule.Aggregate);
            var sum = new Call(Fn.Sum, [_translator.ColumnOf(column.Body)]);
            var tree = new Select([new Call(Fn.Coalesce, [sum, new IntLit(0)])], new TableRef(_table), WhereNode());
            return System.Convert.ToInt64(await _exec.ScalarAsync(CompileTree(tree)) ?? 0L);
        }

        internal Select BuildScalar(Expression<Func<T, object?>> column) =>
            _select.BuildScalar([_translator.MaskedColumnOf(column.Body)], WhereNode());

        public Statement CompileScalar(Expression<Func<T, object?>> column) => CompileTree(BuildScalar(column));

        public string RenderScalar(Expression<Func<T, object?>> column) => CompileScalar(column).Text;

        public async Task<TCol> ScalarAsync<TCol>(Expression<Func<T, object?>> column)
        {
            var v = await _exec.ScalarAsync(CompileScalar(column));
            return ScalarValue.To<TCol>(v, _table, _translator.ColumnNameOf(column.Body));
        }

        public async Task<bool> AnyAsync()
        {
            RefuseClauses("AnyAsync", ClauseRule.Aggregate);
            return await CountRowsAsync() > 0;
        }

        public Query<T> AggregateCount()
        { _aggregates.AddCount(); return this; }

        public Query<T> AggregateCountDistinct(Expression<Func<T, object?>> column)
        { _aggregates.AddCountDistinct(column.Body); return this; }

        public Query<T> AggregateSum(Expression<Func<T, object?>> column)
        { _aggregates.AddSum(column.Body); return this; }

        public Query<T> AggregateMin(Expression<Func<T, object?>> column)
        { _aggregates.AddMin(column.Body); return this; }

        public Query<T> AggregateMax(Expression<Func<T, object?>> column)
        { _aggregates.AddMax(column.Body); return this; }

        public Query<T> AggregateAvg(Expression<Func<T, object?>> column)
        { _aggregates.AddAvg(column.Body); return this; }

        public Query<T> AggregateAvgDecimal(Expression<Func<T, object?>> column, int scale)
        { _aggregates.AddAvgDecimal(column.Body, scale); return this; }

        [RequiresCapability(Capability.AggregateFilter)]
        public Query<T> AggregateCountWhere(Expression<Func<T, bool>> filter)
        { _aggregates.AddCountWhere(filter); return this; }

        public Statement CompileAggregates() => CompileTree(BuildAggregates());

        public string RenderAggregates() => CompileAggregates().Text;

        internal Select BuildAggregates()
        {
            if (_aggregates.Count == 0)
                throw new InvalidOperationException("AggregatesAsync needs at least one aggregate term.");
            RefuseClauses("AggregatesAsync", ClauseRule.Aggregate);
            return new Select(_aggregates.Terms, new TableRef(_table), WhereNode());
        }

        public async Task<object?[]> AggregatesAsync() =>
            _aggregates.Exact(await _exec.AggregateRowAsync(CompileAggregates(), _aggregates.Count));

        public Grouping<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> key) =>
            new(Host(), GroupKeys.Of(_translator, key.Body), r => r.Read<TKey>(0));

        public Grouping<T, (TFirst, TSecond)> GroupBy<TFirst, TSecond>(
            Expression<Func<T, TFirst>> first, Expression<Func<T, TSecond>> second) =>
            new(Host(), GroupKeys.Of(_translator, first.Body, second.Body),
                r => (r.Read<TFirst>(0), r.Read<TSecond>(1)));

        public Grouping<T, (TFirst, TSecond, TThird)> GroupBy<TFirst, TSecond, TThird>(
            Expression<Func<T, TFirst>> first,
            Expression<Func<T, TSecond>> second,
            Expression<Func<T, TThird>> third) =>
            new(Host(),
                GroupKeys.Of(_translator, first.Body, second.Body, third.Body),
                r => (r.Read<TFirst>(0), r.Read<TSecond>(1), r.Read<TThird>(2)));

        private GroupingHost Host()
        {
            RefuseClauses("GroupBy", ClauseRule.Grouping);
            return new(_translator, _state, _exec, _db.Provider, _table, () => WhereNode(),
                () => RefuseClauses("GroupBy", ClauseRule.Grouping));
        }

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
            var query = sub.BuildProject(new[] { selected });
            _where.Add(new InSubquery(_translator.PlainColumnOf(column.Body), query));
            _params.AddRange(sub._params);
            return this;
        }

        public Statement CompileInsertMany(IReadOnlyList<Row<T>> rows) =>
            CompileWrite(_rows.InsertMany(rows).Tree);

        public (string Sql, List<object?> Parameters) RenderInsertMany(IReadOnlyList<Row<T>> rows)
        {
            var built = _rows.InsertMany(rows);
            return (CompileWrite(built.Tree).Text, built.Bound);
        }

        public async Task<int> InsertManyAsync(IReadOnlyList<Row<T>> rows)
        {
            var chunks = _rows.InsertChunks(rows, _state.MaxBindParameters);
            return await _exec.InsertManyAsync(chunks.Select(chunk => CompileWrite(chunk.Tree)).ToList());
        }

        public Query<T> Set(Expression<Func<T, object?>> column, object? value)
        { _set.Value(column.Body, value); return this; }

        public Query<T> SetNow(Expression<Func<T, object?>> column)
        { _set.Now(column.Body); return this; }

        public Query<T> SetCoalesce(Expression<Func<T, object?>> column, object? value)
        { _set.CoalesceValue(column.Body, value); return this; }

        [RequiresCapability(Capability.IntervalArithmetic)]
        public Query<T> SetNowPlusMinutes(Expression<Func<T, object?>> column, int minutes)
        { _set.NowPlusMinutes(column.Body, minutes); return this; }

        public Query<T> SetIncrement(Expression<Func<T, object?>> column, int amount)
        { _set.Increment(column.Body, amount); return this; }

        public Query<T> SetColumn(Expression<Func<T, object?>> column, Expression<Func<T, object?>> source)
        { _set.FromColumn(column.Body, source.Body); return this; }

        public Query<T> SetPredicate(Expression<Func<T, object?>> column, Expression<Func<T, bool>> value)
        { _set.FromPredicate(column.Body, value); return this; }

        [RequiresCapability(Capability.JsonDocument)]
        public Query<T> SetJsonMerge(Expression<Func<T, object?>> document, Jsonb patch)
        {
            _json.Merge(document, patch);
            return this;
        }

        [RequiresCapability(Capability.JsonDocument)]
        public Query<T> SetJsonRemoveThenMerge(
            Expression<Func<T, object?>> document,
            IReadOnlyList<MovedFieldKey> clearedKeys,
            IReadOnlyList<MovedFieldKey> setKeys,
            Jsonb patch)
        {
            _json.RemoveThenMerge(document, clearedKeys, setKeys, patch);
            return this;
        }

        [RequiresCapability(Capability.JsonDocument)]
        public Query<T> SetJsonMemberIncrement(
            Expression<Func<T, object?>> document, MovedFieldKey key, int amount)
        {
            _json.MemberIncrement(document, key, amount);
            return this;
        }

        public Statement CompileUpdate()
        {
            if (_setClauses.Count == 0)
                throw new InvalidOperationException("UpdateAsync requires at least one Set/SetNow.");
            if (_where.Count == 0)
                throw new InvalidOperationException("UpdateAsync requires a Where (refusing an unfiltered UPDATE).");
            return CompileQueryWrite(
                "UpdateAsync", new Update(new TableRef(_table), _setClauses.Assignments, WhereNode()));
        }

        public string RenderUpdate() => CompileUpdate().Text;

        public async Task<int> UpdateAsync() =>
            await _exec.NonQueryAsync(CompileUpdate());

        public Statement CompileDelete()
        {
            if (_where.Count == 0)
                throw new InvalidOperationException("DeleteAsync requires a Where (refusing an unfiltered DELETE).");
            return CompileQueryWrite("DeleteAsync", _compiler.DeleteTree(WhereNode(LiveScope.Always)));
        }

        public string RenderDelete() => CompileDelete().Text;

        public async Task<int> DeleteAsync() =>
            await _exec.NonQueryAsync(CompileDelete());

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Statement CompilePurge()
        {
            if (_where.Count == 0)
                throw new InvalidOperationException("PurgeAsync requires a Where (refusing an unfiltered DELETE).");
            return CompileQueryWrite("PurgeAsync", new Delete(new TableRef(_table), WhereNode(LiveScope.Never)));
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string RenderPurge() => CompilePurge().Text;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task<int> PurgeAsync() =>
            await _exec.NonQueryAsync(CompilePurge());

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Statement CompilePurge(IReadOnlyCollection<Guid> ids, Guid? tenantId) =>
            CompileWrite(_writes.PurgeByKey(ids, tenantId));

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string RenderPurge(IReadOnlyCollection<Guid> ids, Guid? tenantId) =>
            CompilePurge(ids, tenantId).Text;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task<int> PurgeAsync(IReadOnlyCollection<Guid> ids, Guid? tenantId) =>
            await _exec.NonQueryAsync(CompilePurge(ids, tenantId));

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Statement CompileRestore(IReadOnlyCollection<Guid> ids, Guid? tenantId) =>
            CompileWrite(_writes.Restore(ids, tenantId));

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string RenderRestore(IReadOnlyCollection<Guid> ids, Guid? tenantId) =>
            CompileRestore(ids, tenantId).Text;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task<int> RestoreAsync(IReadOnlyCollection<Guid> ids, Guid? tenantId) =>
            await _exec.NonQueryAsync(CompileRestore(ids, tenantId));

        private Statement CompileWrite(Node tree) => _compiler.Write(tree);

        private Statement CompileQueryWrite(string operation, Node tree) => _compiler.QueryWrite(operation, tree);

        private void RefuseClauses(string operation, ClauseRule rule) => ClauseGuard.Require(_select, operation, rule);

        public IReadOnlyList<object?> BoundParameters => _params;

        public IReadOnlyList<string> WhereClauses => WhereTerms.Select(_state.Text).ToList();

        public IReadOnlyList<Node> WhereTerms => _where;

        public void Bind(DbCommand cmd) => _exec.Bind(cmd);

        public Statement CompileSelect() => CompileTree(BuildSelect());

        public string RenderSelect() => CompileSelect().Text;

        internal static Query<T> Definition(ISqlProvider provider)
        {
            var query = new Query<T>(
                OrmContext.Detached(provider),
                static _ => throw new NotSupportedException("a view definition maps no rows"));
            query._state.Qualified = true;
            return query;
        }

        internal string SourceTable => _table;

        internal RenderState State => _state;

        internal PredicateTranslator Translator => _translator;

        internal Select BuildView(IEnumerable<Node> terms, NodeList? joins) =>
            _select.Build(terms, WhereNode()) with { Joins = joins };

        public ViewDefinition AsView<TView>(Action<ViewColumns<T, TView>> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);
            var projection = ViewColumns<T, TView>.Define(this, columns);
            return new ViewDefinition(OrmMeta.Get(typeof(TView)).Table, projection.Build());
        }

        internal GraphQueryState<T> GraphSnapshot() => new(_db, BuildSelect(), _map)
        {
            Connection = _exec.GraphConnection,
            Transaction = _exec.GraphTransaction,
            Masks = new Dictionary<string, Node>(_state.Redactions),
            Unsupported = _projection.Count != 0,
        };

        internal GraphQueryState<T> GraphFirstSnapshot() =>
            QueryPolicyReads<T>.FirstSnapshot(GraphSnapshot());

        internal Select BuildSelect() => _select.Build(SelectedColumns(), WhereNode());

        private Statement CompileTree(Node tree) =>
            StatementCompiler.Compile(tree, _db.Provider, StatementKind.Read, _table);

        private IEnumerable<Node> SelectedColumns() => Redaction.Selected(_state, typeof(T));
    }
