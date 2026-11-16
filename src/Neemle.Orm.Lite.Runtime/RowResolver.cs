#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Neemle.Orm.Lite;

    internal static class RowResolver
    {
        internal static string RawNameOf<T>(PredicateTranslator translator, Expression<Func<T, object?>> column)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            if (!PredicateTranslator.IsColumn(column.Body))
                throw new ArgumentException(
                    "a column lambda must select a member of its parameter, as in e => e.Name.", nameof(column));
            return translator.RawColumnOf(column.Body);
        }

        internal static IReadOnlyList<string> ResolveColumns<T>(
            PredicateTranslator translator, IReadOnlyList<Expression<Func<T, object?>>>? columns) =>
            columns is null ? System.Array.Empty<string>() : columns.Select(c => RawNameOf(translator, c)).ToList();

        internal static IReadOnlyList<string> ResolveRegistered<T>(
            PredicateTranslator translator, IReadOnlyList<Expression<Func<T, object?>>>? columns)
        {
            var names = ResolveColumns(translator, columns);
            var known = OrmMeta.Get(typeof(T)).Columns;
            foreach (var name in names)
                if (known.IndexOf(name) < 0)
                    throw new ArgumentException(
                        "\"" + name + "\" is not a registered column of " + typeof(T).Name
                        + "; a RETURNING list may name only generated columns, never a SQL fragment.",
                        nameof(columns));
            return names;
        }

        internal static ResolvedRow ResolveRow<T>(PredicateTranslator translator, Row<T> row)
        {
            if (row is null) throw new ArgumentNullException(nameof(row));
            var values = new Dictionary<string, object?>();
            foreach (var (column, value) in row.Values) values.Add(RawNameOf(translator, column), value);
            return new ResolvedRow(values, ResolveRegistered(translator, row.Clock));
        }

        internal static IReadOnlyList<UpsertSet> ResolveSets<T>(
            PredicateTranslator translator, ResolvedRow row, IReadOnlyList<UpsertSet<T>>? sets)
        {
            if (sets is null) return System.Array.Empty<UpsertSet>();
            var resolved = new List<UpsertSet>(sets.Count);
            foreach (var set in sets)
            {
                var column = RawNameOf(translator, set.Column);
                if (set.IncrementBy is not null) resolved.Add(UpsertSet.Increment(column, set.IncrementBy.Value));
                else if (set.IsNow) resolved.Add(UpsertSet.Now(column));
                else if (row.Values.ContainsKey(column)) resolved.Add(UpsertSet.ToParam(column, 0));
                else
                    throw new ArgumentException(
                        "UpsertSet.Inserted names the column \"" + column + "\", which the Row does not carry.",
                        nameof(sets));
            }
            return resolved;
        }

        internal static List<ResolvedRow> ResolveInsertRows<T>(
            PredicateTranslator translator, IReadOnlyList<Row<T>> rows)
        {
            if (rows is null) throw new ArgumentNullException(nameof(rows));
            var resolved = rows.Select(r => ResolveRow(translator, r)).ToList();
            foreach (var r in resolved)
                if (!r.Clock.SequenceEqual(resolved[0].Clock))
                    throw new ArgumentException(
                        "every row of an InsertManyAsync must name the SAME database-clock columns.", nameof(rows));
            foreach (var r in resolved)
            {
                if (r.Values.Count != resolved[0].Values.Count)
                    throw new ArgumentException(
                        "every row of an InsertManyAsync must name the SAME columns; a row naming a different "
                        + "set would bind a value into the wrong column rather than fail.", nameof(rows));
                foreach (var c2 in resolved[0].Values.Keys)
                    if (!r.Values.ContainsKey(c2))
                        throw new ArgumentException(
                            "\"" + c2 + "\" is missing from a row of an InsertManyAsync; every row must name the "
                            + "SAME columns in the same order.", nameof(rows));
            }
            return resolved;
        }
    }
