#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Neemle.OrmLite;

    // Turns the typed rows and column lambdas of a query's upsert and bulk insert into the resolved rows the
    // statement builders take, and builds the write tree from them.
    internal sealed class RowWrites<T>
    {
        private readonly PredicateTranslator _translator;
        private readonly WriteStatements _writes;

        internal RowWrites(PredicateTranslator translator, WriteStatements writes)
        {
            _translator = translator;
            _writes = writes;
        }

        internal IReadOnlyList<string> Registered(IReadOnlyList<Expression<Func<T, object?>>>? columns) =>
            RowResolver.ResolveRegistered(_translator, columns);

        internal BuiltWrite Upsert(
            Row<T> row,
            IReadOnlyList<Expression<Func<T, object?>>> conflictColumns,
            OnConflict action,
            IReadOnlyList<UpsertSet<T>>? sets,
            IReadOnlyList<Expression<Func<T, object?>>>? returning)
        {
            var resolved = RowResolver.ResolveRow(_translator, row);
            return _writes.Upsert(
                resolved, RowResolver.ResolveColumns(_translator, conflictColumns), action,
                RowResolver.ResolveSets(_translator, resolved, sets), Registered(returning));
        }

        internal BuiltWrite InsertMany(IReadOnlyList<Row<T>> rows) =>
            _writes.InsertMany(RowResolver.ResolveInsertRows(_translator, rows));

        internal IEnumerable<BuiltWrite> InsertChunks(IReadOnlyList<Row<T>> rows, int capacity) =>
            InsertManyChunks.Split(RowResolver.ResolveInsertRows(_translator, rows), capacity)
                .Select(chunk => _writes.InsertMany(chunk));
    }
