#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

    // What Redact and RedactWhen hide. The mask reaches every read that returns a column row by row (the rows, a
    // projection, a scalar); a read that would aggregate the column, group by it or take its Min hands back what the
    // mask hides, so it is refused by name before anything is rendered or executed.
    internal static class Redaction
    {
        internal static void Hide(RenderState state, PredicateTranslator translator, Expression column) =>
            state.Redactions[translator.RawColumnOf(column)] = new NullLit();

        internal static void HideWhen(
            RenderState state, PredicateTranslator translator, Expression column, LambdaExpression when)
        {
            var raw = translator.RawColumnOf(column);
            var cond = translator.Predicate(when.Parameters[0], when.Body);
            state.Redactions[raw] = new CaseWhen(cond, new NullLit(), new ColumnRef(raw));
        }

        internal static IEnumerable<Node> Selected(RenderState state, Type entity)
        {
            var descriptor = OrmMeta.ReadDescriptor(entity);
            if (descriptor is { Calculated.Count: > 0 })
                return state.Redactions.Count == 0
                    ? EntityProjection.Selected(descriptor)
                    : EntityProjection.Redacted(descriptor, state.Redactions);
            return state.Redactions.Count == 0
                ? [new Star()]
                : OrmMeta.Get(entity).Columns.Select(c => Masked(state, c)).ToList();
        }

        private static Node Masked(RenderState state, string column) =>
            state.Redactions.TryGetValue(column, out var expr) ? new Aliased(expr, column) : new ColumnRef(column);

        internal static Node Mask(RenderState state, Expression column, Func<Node> plain)
        {
            if (state.Redactions.Count == 0) return plain();
            ComparisonOperands.RequireColumn(column);
            return state.Redactions.TryGetValue(state.Col(NameOf(column)), out var mask) ? mask : plain();
        }

        internal static void RefuseRead(RenderState state, Expression column)
        {
            if (state.Redactions.Count == 0) return;
            ComparisonOperands.RequireColumn(column);
            if (!state.Redactions.ContainsKey(state.Col(NameOf(column)))) return;
            throw new NotSupportedException(
                $"Cannot aggregate or group by the column {NameOf(column)}: Redact masks it in the rows this query "
                + "returns, and an aggregate, a grouping key or a Min over it would hand back what the mask hides. "
                + "Nothing was executed. Aggregate a column that is not masked, or read it from a query "
                + "without Redact.");
        }

        private static string NameOf(Expression column)
        {
            while (column is UnaryExpression { NodeType: ExpressionType.Convert } convert) column = convert.Operand;
            return column is MemberExpression member ? member.Member.Name : "";
        }
    }
