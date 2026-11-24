#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Neemle.Orm.Lite;

internal static class ClosureUnary
{
    private static readonly ConcurrentDictionary<
        (ExpressionType Kind, Type Operand, Type Result, MethodInfo? Method),
        Func<object?, object?>> Cache = new();

    internal static bool Handles(UnaryExpression u) =>
        u.NodeType is ExpressionType.Negate or ExpressionType.NegateChecked or ExpressionType.Not
            or ExpressionType.OnesComplement or ExpressionType.UnaryPlus or ExpressionType.Convert
            or ExpressionType.ConvertChecked or ExpressionType.TypeAs or ExpressionType.ArrayLength
        && (u.Method is null || IsOperator(u.Method))
        && !ByRefLike(u.Operand.Type, u.Type);

    internal static object? Apply(UnaryExpression u, object? operand)
    {
        if (IsIdentity(u)) return operand;
        var key = (u.NodeType, u.Operand.Type, u.Type, u.Method);
        var run = Cache.GetOrAdd(key, static k => Build(k.Kind, k.Operand, k.Result, k.Method));
        return run(operand);
    }

    // A Convert to or from a ref struct (C# 14 turns `array.Contains(x)` into a call over the implicit conversion
    // string[] -> ReadOnlySpan<string>) can be neither boxed nor run through a compiled delegate over object. It is
    // forwarded as its operand, which is what the closure value always was for it.
    internal static bool Forwards(UnaryExpression u) =>
        u.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked && ByRefLike(u.Operand.Type, u.Type);

    // ORML-66 (n3): the hand-built call form of a user conversion, `Expression.Call(op_Implicit, operand)`. It is gated
    // like a unary with a Method (a static special-name op_ method) and RUNS the conversion; it no longer forwards its
    // argument unconverted, except across a ref struct (ForwardsCall).
    internal static bool HandlesCall(MethodCallExpression call) =>
        IsConversionCall(call) && !ByRefLike(call.Arguments[0].Type, call.Method.ReturnType);

    internal static bool ForwardsCall(MethodCallExpression call) =>
        IsConversionCall(call) && ByRefLike(call.Arguments[0].Type, call.Method.ReturnType);

    internal static object? ApplyCall(MethodCallExpression call, object? operand) =>
        Apply(Expression.Convert(call.Arguments[0], call.Method.ReturnType, call.Method), operand);

    private static bool IsConversionCall(MethodCallExpression call) =>
        call.Object is null && call.Arguments.Count == 1 && IsOperator(call.Method)
        && call.Method.Name is "op_Implicit" or "op_Explicit";

    private static bool ByRefLike(Type from, Type to) => from.IsByRefLike || to.IsByRefLike;

    private static bool IsOperator(MethodInfo method) =>
        method.IsStatic && method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal);

    private static bool IsIdentity(UnaryExpression u) =>
        u.NodeType == ExpressionType.Convert && u.Method is null && u.Type.IsAssignableFrom(u.Operand.Type);

    private static Func<object?, object?> Build(ExpressionType kind, Type operand, Type result, MethodInfo? method)
    {
        var value = Expression.Parameter(typeof(object), "value");
        var node = Expression.MakeUnary(kind, Expression.Convert(value, operand), result, method);
        var body = Expression.Convert(node, typeof(object));
        return Expression.Lambda<Func<object?, object?>>(body, value).Compile(preferInterpretation: true);
    }
}
