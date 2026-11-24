#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    internal sealed class PredicateTranslator
    {
        private readonly RenderState _state;

        internal PredicateTranslator(RenderState state)
        {
            _state = state;
        }

        internal Node Predicate(ParameterExpression root, Expression body)
        {
            _state.Scope[root] = (_state.Qualified ? new TableRef(_state.Table) : null, _state.Col);
            return _state.Scoped([root], _state.AliasMark, () => Translate(body));
        }

        internal Node Translate(Expression e)
        {
            switch (e)
            {
                case BinaryExpression b:
                    return TranslateBinary(b);
                case UnaryExpression u when u.NodeType is ExpressionType.Not or ExpressionType.Convert:
                    return TranslateUnary(u);
                case MemberExpression m when IsColumn(m):
                    return new Compare(CompareOp.Equal, Col(m), new BoolLit(true));
                case MethodCallExpression mc:
                    return Call(mc);
                default:
                    throw new NotSupportedException("Unsupported predicate: " + e);
            }
        }

        private Node TranslateBinary(BinaryExpression b)
        {
            switch (b.NodeType)
            {
                case ExpressionType.AndAlso:
                    return new And(Translate(b.Left), Translate(b.Right));
                case ExpressionType.OrElse:
                    return new Or(Translate(b.Left), Translate(b.Right));
                case ExpressionType.Equal:
                    return Comparison(b.Left, b.Right, CompareOp.Equal);
                case ExpressionType.NotEqual:
                    return Comparison(b.Left, b.Right, CompareOp.NotEqual);
                case ExpressionType.LessThan:
                    return Relational(b, CompareOp.Less);
                case ExpressionType.LessThanOrEqual:
                    return Relational(b, CompareOp.LessOrEqual);
                case ExpressionType.GreaterThan:
                    return Relational(b, CompareOp.Greater);
                case ExpressionType.GreaterThanOrEqual:
                    return Relational(b, CompareOp.GreaterOrEqual);
                default:
                    throw new NotSupportedException("Unsupported predicate: " + b);
            }
        }

        // A range over an enum column would rank the stored member NAMES, so it is refused before it is built.
        private Node Relational(BinaryExpression b, CompareOp op)
        {
            EnumOrdering.RefuseRelational(b.Left, b.Right, op);
            return Comparison(b.Left, b.Right, op);
        }

        private Node TranslateUnary(UnaryExpression u)
        {
            if (u.NodeType == ExpressionType.Convert) return Translate(u.Operand);
            return Unwrap(u.Operand) is MemberExpression nm && IsColumn(nm)
                ? new Compare(CompareOp.Equal, Col(nm), new BoolLit(false))
                : new Not(Translate(u.Operand));
        }

        private Node Call(MethodCallExpression mc)
        {
            var rendered = CallDb(mc);
            if (rendered is not null) return rendered;
            var like = StringLikeColumn(mc);
            if (like is not null) return CallStringLike(mc, like);
            if (mc.Method.Name == "Contains" && mc.Arguments.Count >= 1
                && IsColumn(mc.Arguments[mc.Arguments.Count - 1]))
                return CallIn(mc);
            throw new NotSupportedException("Unsupported call: " + mc.Method.Name);
        }

        private Node? CallDb(MethodCallExpression mc)
        {
            if (mc.Method.DeclaringType != typeof(Db)) return null;
            switch (mc.Method.Name)
            {
                case "Exists": return TranslateExists(mc);
                case "AnyOf": return TranslateAnyOf(mc);
                case "NullSafeEquals": return TranslateNullSafeEquals(mc);
                case "EqualsIgnoreCase": return TranslateEqualsIgnoreCase(mc);
                default: return CallDbJson(mc) ?? CallDbMoved(mc);
            }
        }

        private Node? CallDbJson(MethodCallExpression mc)
        {
            switch (mc.Method.Name)
            {
                case "JsonKeyEquals": return TranslateJsonKeyEquals(mc);
                case "JsonHasKey": return TranslateJsonHasKey(mc);
                case "JsonArrayContainsString": return TranslateJsonArrayContainsString(mc);
                case "JsonArrayContainsAny": return TranslateJsonArrayContainsAny(mc);
                default: return null;
            }
        }

        private Node? CallDbMoved(MethodCallExpression mc)
        {
            switch (mc.Method.Name)
            {
                case "MovedFieldIsNull": return new IsNull(MovedFieldResolved(mc));
                case "MovedFieldEquals": return TranslateMovedFieldEquals(mc);
                case "MovedFieldContains": return TranslateMovedFieldContains(mc);
                default: return null;
            }
        }

        private static MemberExpression? StringLikeColumn(MethodCallExpression mc)
        {
            if (mc.Method.Name is not ("Contains" or "StartsWith" or "EndsWith")) return null;
            if (mc.Object is null || mc.Arguments.Count < 1 || mc.Arguments[0].Type != typeof(string)) return null;
            return Unwrap(mc.Object) is MemberExpression column && IsColumn(column) ? column : null;
        }

        private Node CallStringLike(MethodCallExpression mc, MemberExpression column)
        {
            var parameter = _state.Bind(StringLike.Pattern(mc.Method.Name, Eval(mc.Arguments[0])));
            return StringLike.IgnoresCase(mc, Eval)
                ? new ILike(Col(column), parameter, LikePattern.EscapeChar)
                : new Like(Col(column), parameter, LikePattern.EscapeChar);
        }

        private Node CallIn(MethodCallExpression mc)
        {
            var column = (MemberExpression)Unwrap(mc.Arguments[mc.Arguments.Count - 1]);
            var values = InValues.Of(_state, Eval(mc.Object ?? mc.Arguments[0]), column.Type);
            if (values.Count == 0) return new BoolLit(false);
            var items = values.Select(value => (Node)_state.Bind(value)).ToList();
            return new InList(Col(column), NodeList.Of(items));
        }

        private Node TranslateAnyOf(MethodCallExpression mc)
        {
            if (!_state.Allows(Capability.ArrayParameter))
                throw _state.Refusal(Capability.ArrayParameter, "Db.AnyOf",
                    $"{_state.ProviderName} has no array parameter; express this as Contains, "
                    + "which expands to one placeholder per element.");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.AnyOf's first argument must be a column of the entity being queried.");
            var values = Eval(mc.Arguments[1]);
            if (values is not ICollection)
                throw new NotSupportedException(
                    "Db.AnyOf binds its collection as ONE array parameter, so it must already be materialised "
                    + "-- pass an array or a List<T>, never a lazy sequence, because the driver infers the "
                    + "array's element type from the object it is handed.");
            var parameter = _state.Bind(values);
            return new AnyOf(Col((MemberExpression)Unwrap(mc.Arguments[0])), parameter);
        }

        private Node TranslateNullSafeEquals(MethodCallExpression mc)
        {
            if (!_state.Allows(Capability.NullSafeEquality))
                throw _state.Refusal(Capability.NullSafeEquality, "Db.NullSafeEquals",
                    $"{_state.ProviderName} has no null-safe equality; rewriting it as = would silently stop "
                    + "matching NULL scopes.");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.NullSafeEquals's first argument must be a column of the entity being queried.");
            var parameter = _state.Bind(Eval(mc.Arguments[1]));
            return new NullSafeEquals(Col((MemberExpression)Unwrap(mc.Arguments[0])), parameter);
        }

        private Node TranslateEqualsIgnoreCase(MethodCallExpression mc)
        {
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.EqualsIgnoreCase's first argument must be a column of the entity being queried.");
            var parameter = _state.Bind(Eval(mc.Arguments[1]));
            return new Compare(
                CompareOp.Equal,
                new Call(Fn.Lower, [Col((MemberExpression)Unwrap(mc.Arguments[0]))]),
                new Call(Fn.Lower, [parameter]));
        }

        private Node TranslateJsonKeyEquals(MethodCallExpression mc)
        {
            _state.RequireJsonDocument("filter on a key inside a JSON document");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.JsonKeyEquals's first argument must be a column of the entity being queried.");
            var key = RenderState.RequireKey(Eval(mc.Arguments[1]) as string
                ?? throw new ArgumentNullException("key", "a JSON key must be a string."));
            var value = Eval(mc.Arguments[2]) as string
                ?? throw new ArgumentNullException(
                    "value",
                    "Db.JsonKeyEquals compares the key's extracted VALUE, and comparing it to NULL matches "
                    + "no row at all rather than refusing. Use Db.JsonHasKey to ask whether the document "
                    + "carries the key -- it answers differently for an absent key and for a key whose "
                    + "value is JSON null, which this comparison cannot.");
            var order = Eval(mc.Arguments[3]) is JsonValueOrder supplied
                ? supplied
                : throw new NotSupportedException(
                    "Db.JsonKeyEquals's fourth argument states HOW the key is compared and must be an "
                    + "evaluable JsonValueOrder. Comparing a number field's value as text is what put 10 "
                    + "before 2 and made \"10\" miss a stored 10.0, so the marker no longer has a form "
                    + "that leaves the question unanswered.");
            var document = Col((MemberExpression)Unwrap(mc.Arguments[0]));
            var keyParameter = _state.Bind(key);
            var valueParameter = _state.Bind(value);
            return new Compare(
                CompareOp.Equal,
                new Comparable(new JsonKeyText(document, keyParameter), order),
                new Comparable(valueParameter, order));
        }

        private Node MovedFieldResolved(MethodCallExpression mc)
        {
            _state.RequireJsonDocument("resolve a moved field against a JSON document");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db." + mc.Method.Name + "'s document argument must be the JSON document column of the "
                    + "entity being queried.");
            if (Eval(mc.Arguments[1]) is not MovedFieldKey key)
                throw new NotSupportedException(
                    "Db." + mc.Method.Name + "'s key argument must be a MovedFieldKey this builder can "
                    + "evaluate -- one of the keys minted once per moved field -- because the key is "
                    + "rendered into the statement as a literal so that the expression index over the "
                    + "same literal can be matched.");
            return new JsonKeyText(PlainColumnOf(mc.Arguments[0]), new StringLit(key.Name));
        }

        private Node TranslateMovedFieldEquals(MethodCallExpression mc)
        {
            var resolved = MovedFieldResolved(mc);
            var value = Eval(mc.Arguments[2])
                ?? throw new ArgumentNullException(
                    "value",
                    "Db.MovedFieldEquals compares the resolved value and comparing it to NULL matches no "
                    + "row at all rather than refusing. Use Db.MovedFieldIsNull, which renders IS NULL "
                    + "and answers for a document that says null AND for an absent member -- two "
                    + "populations an equality can never reach.");
            var parameter = _state.Bind(value);
            return new Compare(CompareOp.Equal, resolved, new Cast(parameter, CastType.Text));
        }

        private Node TranslateMovedFieldContains(MethodCallExpression mc)
        {
            var resolved = MovedFieldResolved(mc);
            var term = Eval(mc.Arguments[2]) as string
                ?? throw new ArgumentNullException(
                    "value", "Db.MovedFieldContains matches a substring and has none to match.");
            var parameter = _state.Bind(LikePattern.Contains(term));
            return new ILike(resolved, parameter, LikePattern.EscapeChar);
        }

        private Node TranslateJsonHasKey(MethodCallExpression mc)
        {
            _state.RequireJsonDocument("test for a key inside a JSON document");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.JsonHasKey's first argument must be a column of the entity being queried.");
            var key = RenderState.RequireKey(Eval(mc.Arguments[1]) as string
                ?? throw new ArgumentNullException("key", "a JSON key must be a string."));
            var document = Col((MemberExpression)Unwrap(mc.Arguments[0]));
            return new JsonKeyExists(document, _state.Bind(key));
        }

        private Node TranslateJsonArrayContainsString(MethodCallExpression mc)
        {
            _state.RequireJsonDocument("test array membership inside a JSON document");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.JsonArrayContainsString's first argument must be a column of the entity being queried.");
            var key = RenderState.RequireKey(Eval(mc.Arguments[1]) as string
                ?? throw new ArgumentNullException("key", "a JSON key must be a string."));
            var value = Eval(mc.Arguments[2]) as string
                ?? throw new ArgumentNullException(
                    "value",
                    "Db.JsonArrayContainsString tests for exact-string array membership, and a null value "
                    + "would bind SQL NULL -- a JSON array-membership test against a NULL argument matches no "
                    + "row at all rather than refusing.");
            var document = Col((MemberExpression)Unwrap(mc.Arguments[0]));
            var keyParameter = _state.Bind(key);
            var valueParameter = _state.Bind(value);
            return new JsonArrayContainsString(document, keyParameter, valueParameter);
        }

        private Node TranslateJsonArrayContainsAny(MethodCallExpression mc)
        {
            _state.RequireJsonDocument("test array membership inside a JSON document");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.JsonArrayContainsAny's first argument must be a column of the entity being queried.");
            var key = RenderState.RequireKey(Eval(mc.Arguments[1]) as string
                ?? throw new ArgumentNullException("key", "a JSON key must be a string."));
            var values = Eval(mc.Arguments[2]) as IReadOnlyList<string>;
            if (values is null || values.Count == 0 || values.Any(v => v is null))
                throw new ArgumentException(
                    "Db.JsonArrayContainsAny needs at least one non-null value to test membership of.", "values");
            var document = Col((MemberExpression)Unwrap(mc.Arguments[0]));
            var keyParameter = _state.Bind(key);
            var valueParameters = new List<Node>(values.Count);
            foreach (var value in values) valueParameters.Add(_state.Bind(value));
            return new JsonArrayContainsAny(document, keyParameter, NodeList.Of(valueParameters));
        }

        private Node TranslateExists(MethodCallExpression mc)
        {
            var lambda = (LambdaExpression)StripQuote(mc.Arguments[0]);
            var inner = lambda.Parameters[0];
            var other = mc.Method.GetGenericArguments()[0];
            var meta = OrmMeta.Get(other);
            var alias = _state.NextAlias();
            var promoted = _state.Scope.Where(kv => kv.Value.Owner is null).Select(kv => kv.Key).ToList();
            foreach (var p in promoted) _state.Scope[p] = (new TableRef(_state.Table), _state.Scope[p].Col);
            _state.Scope[inner] = (new AliasRef(alias), meta.Col);
            Node body;
            try
            {
                body = Translate(lambda.Body);
            }
            finally
            {
                _state.Scope.Remove(inner);
                foreach (var p in promoted) _state.Scope[p] = (null, _state.Scope[p].Col);
            }
            return new Exists(CorrelatedSelect(other, meta.Table, alias, [new IntLit(1)], body));
        }

        internal Select CorrelatedSelect(Type other, string table, string alias, NodeList columns, Node body)
        {
            var live = _state.LiveTermOf(other, alias, OrmMeta.Get(other).Col);
            return new Select(
                columns,
                new TableRef(table, alias),
                new Where(live is null ? [body] : [body, live]));
        }

        private static bool IsNowMarker(Expression e) =>
            Unwrap(e) is MethodCallExpression m && m.Method.DeclaringType == typeof(Db) && m.Method.Name == "Now";

        private Node Comparison(Expression left, Expression right, CompareOp op)
        {
            ComparisonOperands.RequireColumnSide(left, right);
            var lc = IsColumn(left); var rc = IsColumn(right);
            if (lc && rc)
                return new Compare(op, Col((MemberExpression)Unwrap(left)), Col((MemberExpression)Unwrap(right)));
            if (lc && IsNowMarker(right))
                return new Compare(op, Col((MemberExpression)Unwrap(left)), new ClockNow());
            if (rc && IsNowMarker(left))
                return new Compare(op, new ClockNow(), Col((MemberExpression)Unwrap(right)));
            var (colExpr, valExpr) = lc ? (Unwrap(left), right) : (Unwrap(right), left);
            var colMember = (MemberExpression)colExpr;
            var col = Col(colMember);
            var val = Eval(valExpr);
            if (val is null && op == CompareOp.Equal) return new IsNull(col);
            if (val is null && op == CompareOp.NotEqual) return new IsNotNull(col);
            ComparisonOperands.RequireNonNullRelational(op, val, colMember);
            var colType = Nullable.GetUnderlyingType(colMember.Type) ?? colMember.Type;
            if (colType.IsEnum && val is byte or sbyte or short or ushort or int or uint or long or ulong)
                val = Enum.ToObject(colType, val);
            var parameter = _state.Bind(val);
            return new Compare(lc ? op : Mirror(op), col, _state.ValueOf(val, parameter));
        }

        // `3 < n.Rank` is `n.Rank > 3`: the text is always column first, so a value on the left swaps the
        // operator's side.
        private static CompareOp Mirror(CompareOp op) => op switch
        {
            CompareOp.Less => CompareOp.Greater,
            CompareOp.LessOrEqual => CompareOp.GreaterOrEqual,
            CompareOp.Greater => CompareOp.Less,
            CompareOp.GreaterOrEqual => CompareOp.LessOrEqual,
            _ => op,
        };

        // Every conversion around an expression is peeled, so `n.Status`, `(int)n.Status` and the object
        // conversion that a selector adds on top of it all name the same column: a chain of conversions is no
        // reason to call a column something else, and the enum guards (EnumOrdering) peel the same way.
        private static Expression Unwrap(Expression e)
        {
            while (e is UnaryExpression { NodeType: ExpressionType.Convert } u) e = u.Operand;
            return e;
        }
        private static Expression StripQuote(Expression e) =>
            e is UnaryExpression u && u.NodeType == ExpressionType.Quote ? u.Operand : e;
        internal static bool IsColumn(Expression e) =>
            Unwrap(e) is MemberExpression m && m.Expression is ParameterExpression;
        private static ParameterExpression? ParamOf(Expression e) =>
            Unwrap(e) is MemberExpression m && m.Expression is ParameterExpression p ? p : null;

        private ColumnRef Col(MemberExpression m)
        {
            var p = ParamOf(m);
            if (p != null && _state.Scope.TryGetValue(p, out var info))
                return new ColumnRef(info.Col(m.Member.Name), info.Owner);
            return new ColumnRef(_state.Col(m.Member.Name));
        }

        internal static bool IsDecimal(Expression e) =>
            Unwrap(e).Type == typeof(decimal) || Unwrap(e).Type == typeof(decimal?);

        // A column read out as a value (an aggregate, a grouping key): refused when Redact masks it, because the
        // aggregate would hand back what the mask hides.
        internal Node ColumnOf(Expression e)
        {
            Redaction.RefuseRead(_state, e);
            return PlainColumnOf(e);
        }

        // A column as a term of a filter or an order, which a mask does not touch.
        internal Node PlainColumnOf(Expression e)
        {
            ComparisonOperands.RequireColumn(e);
            return Col((MemberExpression)Unwrap(e));
        }

        // A column returned row by row (ProjectAsync, ProjectionAsync, ScalarAsync): Redact's mask replaces it.
        internal Node MaskedColumnOf(Expression e) => Redaction.Mask(_state, e, () => PlainColumnOf(e));

        internal Node OrderColumnOf(Expression e)
        {
            EnumOrdering.RefuseOrder(e);
            return PlainColumnOf(e);
        }

        internal string ColumnNameOf(Expression e)
        {
            ComparisonOperands.RequireColumn(e);
            return Col((MemberExpression)Unwrap(e)).Column;
        }

        internal string RawColumnOf(Expression e)
        {
            ComparisonOperands.RequireColumn(e);
            return _state.Col(((MemberExpression)Unwrap(e)).Member.Name);
        }

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075",
            Justification = "Closure fields/properties captured by the predicate are referenced and preserved.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2080",
            Justification = "Same as IL2075.")]
        private static object? Eval(Expression e)
        {
            switch (e)
            {
                case ConstantExpression c:
                    return c.Value;
                case MemberExpression m:
                    var owner = m.Expression is null ? null : Eval(m.Expression);
                    return m.Member is FieldInfo f ? f.GetValue(owner) : ((PropertyInfo)m.Member).GetValue(owner);
                case UnaryExpression u when ClosureUnary.Forwards(u):
                    return Eval(u.Operand);
                case UnaryExpression u when ClosureUnary.Handles(u):
                    return ClosureUnary.Apply(u, Eval(u.Operand));
                case MethodCallExpression mc when ClosureUnary.ForwardsCall(mc):
                    return Eval(mc.Arguments[0]);
                case MethodCallExpression mc when ClosureUnary.HandlesCall(mc):
                    return ClosureUnary.ApplyCall(mc, Eval(mc.Arguments[0]));
                default:
                    throw new NotSupportedException("Cannot evaluate: " + e);
            }
        }
    }
