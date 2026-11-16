#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Neemle.Orm.Lite;

    internal sealed class PredicateTranslator
    {
        private readonly RenderState _state;
        private readonly ISqlDialect D;
        private readonly List<object?> _params;

        internal PredicateTranslator(RenderState state)
        {
            _state = state;
            D = state.Dialect;
            _params = state.Params;
        }

        internal string Predicate(ParameterExpression root, Expression body)
        {
            _state.Scope[root] = ("", _state.Col);
            var rendered = Translate(body);
            _state.Scope.Remove(root);
            return rendered;
        }

        internal string Translate(Expression e)
        {
            switch (e)
            {
                case BinaryExpression b:
                    return TranslateBinary(b);
                case UnaryExpression u when u.NodeType is ExpressionType.Not or ExpressionType.Convert:
                    return TranslateUnary(u);
                case MemberExpression m when IsColumn(m):
                    return Col(m) + " = true";
                case MethodCallExpression mc:
                    return Call(mc);
                default:
                    throw new NotSupportedException("Unsupported predicate: " + e);
            }
        }

        private string TranslateBinary(BinaryExpression b)
        {
            switch (b.NodeType)
            {
                case ExpressionType.AndAlso:
                    return "(" + Translate(b.Left) + " AND " + Translate(b.Right) + ")";
                case ExpressionType.OrElse:
                    return "(" + Translate(b.Left) + " OR " + Translate(b.Right) + ")";
                case ExpressionType.Equal:
                    return Compare(b.Left, b.Right, "=", "IS NULL");
                case ExpressionType.NotEqual:
                    return Compare(b.Left, b.Right, "<>", "IS NOT NULL");
                case ExpressionType.LessThan:
                    return Compare(b.Left, b.Right, "<", null);
                case ExpressionType.LessThanOrEqual:
                    return Compare(b.Left, b.Right, "<=", null);
                case ExpressionType.GreaterThan:
                    return Compare(b.Left, b.Right, ">", null);
                case ExpressionType.GreaterThanOrEqual:
                    return Compare(b.Left, b.Right, ">=", null);
                default:
                    throw new NotSupportedException("Unsupported predicate: " + b);
            }
        }

        private string TranslateUnary(UnaryExpression u)
        {
            if (u.NodeType == ExpressionType.Convert) return Translate(u.Operand);
            return Unwrap(u.Operand) is MemberExpression nm && IsColumn(nm)
                ? Col(nm) + " = false"
                : "NOT (" + Translate(u.Operand) + ")";
        }

        private string Call(MethodCallExpression mc)
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

        private string? CallDb(MethodCallExpression mc)
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

        private string? CallDbJson(MethodCallExpression mc)
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

        private string? CallDbMoved(MethodCallExpression mc)
        {
            switch (mc.Method.Name)
            {
                case "MovedFieldIsNull": return MovedFieldResolved(mc) + " IS NULL";
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

        private string CallStringLike(MethodCallExpression mc, MemberExpression column)
        {
            var v = Eval(mc.Arguments[0]) as string ?? "";
            var pat = mc.Method.Name == "StartsWith" ? LikePattern.StartsWith(v)
                : mc.Method.Name == "EndsWith" ? LikePattern.EndsWith(v)
                : LikePattern.Contains(v);
            _params.Add(pat);
            var ci = mc.Arguments.Count == 2 && Eval(mc.Arguments[1]) is StringComparison sc && IsIgnoreCase(sc);
            return Col(column) + (ci ? " ILIKE " : " LIKE ") + _state.NextSlot() + LikePattern.EscapeClause;
        }

        private static bool IsIgnoreCase(StringComparison sc) =>
            sc is StringComparison.OrdinalIgnoreCase
                or StringComparison.InvariantCultureIgnoreCase
                or StringComparison.CurrentCultureIgnoreCase;

        private string CallIn(MethodCallExpression mc)
        {
            var collection = Eval(mc.Object ?? mc.Arguments[0]) as IEnumerable
                ?? throw new NotSupportedException("IN requires an evaluable collection.");
            var slots = new List<string>();
            foreach (var it in collection) { _params.Add(it); slots.Add(_state.NextSlot()); }
            if (slots.Count == 0) return "false";
            var column = (MemberExpression)Unwrap(mc.Arguments[mc.Arguments.Count - 1]);
            return Col(column) + " IN (" + string.Join(", ", slots) + ")";
        }

        private string TranslateAnyOf(MethodCallExpression mc)
        {
            if (!D.SupportsArrayParameter)
                throw new NotSupportedException(
                    $"{D.GetType().Name} has no array parameter; express this as Contains, "
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
            _params.Add(values);
            return D.AnyOfExpression(Col((MemberExpression)Unwrap(mc.Arguments[0])), _state.NextSlot());
        }

        private string TranslateNullSafeEquals(MethodCallExpression mc)
        {
            if (!D.SupportsNullSafeEquality)
                throw new NotSupportedException(
                    $"{D.GetType().Name} has no null-safe equality; rewriting it as = would silently stop "
                    + "matching NULL scopes.");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.NullSafeEquals's first argument must be a column of the entity being queried.");
            _params.Add(Eval(mc.Arguments[1]));
            return D.NullSafeEqualsExpression(Col((MemberExpression)Unwrap(mc.Arguments[0])), _state.NextSlot());
        }

        private string TranslateEqualsIgnoreCase(MethodCallExpression mc)
        {
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.EqualsIgnoreCase's first argument must be a column of the entity being queried.");
            _params.Add(Eval(mc.Arguments[1]));
            return "lower(" + Col((MemberExpression)Unwrap(mc.Arguments[0])) + ") = lower(" + _state.NextSlot() + ")";
        }

        private string TranslateJsonKeyEquals(MethodCallExpression mc)
        {
            _state.RequireJsonDocumentDialect("filter on a key inside a JSON document");
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
            var quoted = Col((MemberExpression)Unwrap(mc.Arguments[0]));
            _params.Add(key);
            var keySlot = _state.NextSlot();
            _params.Add(value);
            return D.JsonKeyComparableExpression(quoted, keySlot, order)
                + " = " + D.ComparableParameter(_state.NextSlot(), order);
        }

        private string MovedFieldResolved(MethodCallExpression mc)
        {
            _state.RequireJsonDocumentDialect("resolve a moved field against a JSON document");
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
            return D.MovedFieldResolvedTextExpression(ColumnOf(mc.Arguments[0]), key.Name);
        }

        private string TranslateMovedFieldEquals(MethodCallExpression mc)
        {
            var resolved = MovedFieldResolved(mc);
            var value = Eval(mc.Arguments[2])
                ?? throw new ArgumentNullException(
                    "value",
                    "Db.MovedFieldEquals compares the resolved value and comparing it to NULL matches no "
                    + "row at all rather than refusing. Use Db.MovedFieldIsNull, which renders IS NULL "
                    + "and answers for a document that says null AND for an absent member -- two "
                    + "populations an equality can never reach.");
            _params.Add(value);
            return resolved + " = " + D.TextValueExpression(_state.NextSlot());
        }

        private string TranslateMovedFieldContains(MethodCallExpression mc)
        {
            var resolved = MovedFieldResolved(mc);
            var term = Eval(mc.Arguments[2]) as string
                ?? throw new ArgumentNullException(
                    "value", "Db.MovedFieldContains matches a substring and has none to match.");
            _params.Add(LikePattern.Contains(term));
            return resolved + " ILIKE " + _state.NextSlot() + LikePattern.EscapeClause;
        }

        private string TranslateJsonHasKey(MethodCallExpression mc)
        {
            _state.RequireJsonDocumentDialect("test for a key inside a JSON document");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.JsonHasKey's first argument must be a column of the entity being queried.");
            var key = RenderState.RequireKey(Eval(mc.Arguments[1]) as string
                ?? throw new ArgumentNullException("key", "a JSON key must be a string."));
            var quoted = Col((MemberExpression)Unwrap(mc.Arguments[0]));
            _params.Add(key);
            return D.JsonKeyExistsExpression(quoted, _state.NextSlot());
        }

        private string TranslateJsonArrayContainsString(MethodCallExpression mc)
        {
            _state.RequireJsonDocumentDialect("test array membership inside a JSON document");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.JsonArrayContainsString's first argument must be a column of the entity being queried.");
            var key = RenderState.RequireKey(Eval(mc.Arguments[1]) as string
                ?? throw new ArgumentNullException("key", "a JSON key must be a string."));
            var value = Eval(mc.Arguments[2]) as string
                ?? throw new ArgumentNullException(
                    "value",
                    "Db.JsonArrayContainsString tests for exact-string array membership, and a null value "
                    + "would bind SQL NULL -- jsonb_exists against a NULL argument matches no row at all "
                    + "rather than refusing.");
            var quoted = Col((MemberExpression)Unwrap(mc.Arguments[0]));
            _params.Add(key);
            var keySlot = _state.NextSlot();
            _params.Add(value);
            return D.JsonArrayContainsStringExpression(quoted, keySlot, _state.NextSlot());
        }

        private string TranslateJsonArrayContainsAny(MethodCallExpression mc)
        {
            _state.RequireJsonDocumentDialect("test array membership inside a JSON document");
            if (!IsColumn(mc.Arguments[0]))
                throw new NotSupportedException(
                    "Db.JsonArrayContainsAny's first argument must be a column of the entity being queried.");
            var key = RenderState.RequireKey(Eval(mc.Arguments[1]) as string
                ?? throw new ArgumentNullException("key", "a JSON key must be a string."));
            var values = Eval(mc.Arguments[2]) as IReadOnlyList<string>;
            if (values is null || values.Count == 0 || values.Any(v => v is null))
                throw new ArgumentException(
                    "Db.JsonArrayContainsAny needs at least one non-null value to test membership of.", "values");
            var quoted = Col((MemberExpression)Unwrap(mc.Arguments[0]));
            _params.Add(key);
            var keySlot = _state.NextSlot();
            var slots = new List<string>(values.Count);
            foreach (var value in values)
            {
                _params.Add(value);
                slots.Add(_state.NextSlot());
            }
            return D.JsonArrayContainsAnyExpression(quoted, keySlot, slots);
        }

        private string TranslateExists(MethodCallExpression mc)
        {
            var lambda = (LambdaExpression)StripQuote(mc.Arguments[0]);
            var inner = lambda.Parameters[0];
            var other = mc.Method.GetGenericArguments()[0];
            var meta = OrmMeta.Get(other);
            var alias = _state.NextAlias();
            var promoted = _state.Scope.Where(kv => kv.Value.Q.Length == 0).Select(kv => kv.Key).ToList();
            foreach (var p in promoted) _state.Scope[p] = (D.Quote(_state.Table), _state.Scope[p].Col);
            _state.Scope[inner] = (alias, meta.Col);
            var body = Translate(lambda.Body);
            _state.Scope.Remove(inner);
            foreach (var p in promoted) _state.Scope[p] = ("", _state.Scope[p].Col);
            return "EXISTS (SELECT 1 FROM " + D.Quote(meta.Table) + " " + alias + " WHERE " + body
                + _state.LiveTermOf(other, alias, meta.Col) + ")";
        }

        private static bool IsNowMarker(Expression e) =>
            Unwrap(e) is MethodCallExpression m && m.Method.DeclaringType == typeof(Db) && m.Method.Name == "Now";

        private string Compare(Expression left, Expression right, string op, string? nullOp)
        {
            var lc = IsColumn(left); var rc = IsColumn(right);
            if (lc && rc)
                return Col((MemberExpression)Unwrap(left)) + " " + op + " " + Col((MemberExpression)Unwrap(right));
            if (lc && IsNowMarker(right))
                return Col((MemberExpression)Unwrap(left)) + " " + op + " now()";
            if (rc && IsNowMarker(left))
                return "now() " + op + " " + Col((MemberExpression)Unwrap(right));
            var (colExpr, valExpr) = lc ? (Unwrap(left), right) : (Unwrap(right), left);
            var colMember = (MemberExpression)colExpr;
            var col = Col(colMember);
            var val = Eval(valExpr);
            if (val is null && nullOp != null) return col + " " + nullOp;
            var colType = Nullable.GetUnderlyingType(colMember.Type) ?? colMember.Type;
            if (colType.IsEnum && val is byte or sbyte or short or ushort or int or uint or long or ulong)
                val = Enum.ToObject(colType, val);
            _params.Add(val);
            return col + " " + op + " " + _state.ValueSlot(val, _state.NextSlot());
        }

        private static Expression Unwrap(Expression e) =>
            e is UnaryExpression u && u.NodeType == ExpressionType.Convert ? u.Operand : e;
        private static Expression StripQuote(Expression e) =>
            e is UnaryExpression u && u.NodeType == ExpressionType.Quote ? u.Operand : e;
        internal static bool IsColumn(Expression e) =>
            Unwrap(e) is MemberExpression m && m.Expression is ParameterExpression;
        private static ParameterExpression? ParamOf(Expression e) =>
            Unwrap(e) is MemberExpression m && m.Expression is ParameterExpression p ? p : null;

        private string Col(MemberExpression m)
        {
            var p = ParamOf(m);
            if (p != null && _state.Scope.TryGetValue(p, out var info))
                return info.Q.Length > 0
                    ? info.Q + "." + D.Quote(info.Col(m.Member.Name))
                    : D.Quote(info.Col(m.Member.Name));
            return D.Quote(_state.Col(m.Member.Name));
        }

        internal string ColumnOf(Expression e) => Col((MemberExpression)Unwrap(e));
        internal string RawColumnOf(Expression e) => _state.Col(((MemberExpression)Unwrap(e)).Member.Name);

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075",
            Justification = "Closure fields/properties captured by the predicate are referenced and preserved.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2080",
            Justification = "Same as IL2075.")]
        private static object? Eval(Expression e)
        {
            e = Unwrap(e);
            switch (e)
            {
                case ConstantExpression c:
                    return c.Value;
                case MemberExpression m:
                    var owner = m.Expression is null ? null : Eval(m.Expression);
                    return m.Member is FieldInfo f ? f.GetValue(owner) : ((PropertyInfo)m.Member).GetValue(owner);
                case UnaryExpression u:
                    return Eval(u.Operand);
                case MethodCallExpression mc
                    when mc.Method.Name is "op_Implicit" or "op_Explicit" && mc.Arguments.Count == 1:
                    return Eval(mc.Arguments[0]);
                default:
                    throw new NotSupportedException("Cannot evaluate: " + e);
            }
        }
    }
