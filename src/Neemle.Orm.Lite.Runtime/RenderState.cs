#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Neemle.Orm.Lite;

    internal sealed class RenderState
    {
        private int _aliasCount;

        internal RenderState(ISqlDialect dialect, string table, Func<string, string> col, List<object?> parameters)
        {
            Dialect = dialect;
            Table = table;
            Col = col;
            Params = parameters;
        }

        internal ISqlDialect Dialect { get; }

        internal string Table { get; }

        internal Func<string, string> Col { get; }

        internal List<object?> Params { get; }

        internal int ParamBase { get; set; }

        internal Dictionary<ParameterExpression, (string Q, Func<string, string> Col)> Scope { get; } = new();

        internal string NextAlias() => "s" + _aliasCount++;

        internal string Slot(int index) => Dialect.Param(ParamBase + index);

        internal string NextSlot() => Slot(Params.Count - 1);

        internal void RequireJsonDocumentDialect(string what)
        {
            if (!Dialect.SupportsJsonDocument)
                throw new NotSupportedException(
                    $"{Dialect.GetType().Name} has no JSON document type, so it cannot {what}; a statement "
                    + "emitted anyway would carry PostgreSQL syntax to a server that cannot parse it.");
        }

        internal static string RequireKey(string key) =>
            key ?? throw new ArgumentNullException(
                nameof(key),
                "a JSON key must be a string; a null key binds SQL NULL and every row then fails to "
                + "match, which is an empty result rather than a refusal.");

        internal string ValueSlot(object? value, string slot)
        {
            if (value is not Jsonb) return slot;
            RequireJsonDocumentDialect("bind a JSON document");
            return Dialect.JsonDocumentParameter(slot);
        }

        internal string LiveTermOf(Type other, string alias, Func<string, string> col) =>
            OrmMeta.TryGetSoftDelete(other, out var soft)
                ? " AND " + SoftDeleteSql.LiveTerm(Dialect, col(soft.Column), alias)
                : "";
    }
