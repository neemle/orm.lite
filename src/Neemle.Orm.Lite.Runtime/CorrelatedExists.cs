#nullable enable
using System;
using System.Linq.Expressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    // The EXISTS term of Query<T>.WhereExists: the outer row stays the query's own table, the inner row gets a fresh
    // alias, and the predicate is translated with both in scope.
    internal static class CorrelatedExists
    {
        internal static Node Of(
            RenderState state, PredicateTranslator translator, Type other, LambdaExpression predicate)
        {
            var outer = predicate.Parameters[0];
            var inner = predicate.Parameters[1];
            var meta = OrmMeta.Get(other);
            var mark = state.AliasMark;
            var alias = state.NextAlias();
            state.Scope[outer] = (new TableRef(state.Table), state.Col);
            state.Scope[inner] = (new AliasRef(alias), meta.Col);
            var body = state.Scoped(predicate.Parameters, mark, () => translator.Translate(predicate.Body));
            return new Exists(translator.CorrelatedSelect(other, meta.Table, alias, [new IntLit(1)], body));
        }
    }
