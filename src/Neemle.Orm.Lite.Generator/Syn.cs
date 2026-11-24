using System.Collections.Generic;
using System.Linq;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Model;

namespace Neemle.Orm.Lite;

internal static class Syn
{
    internal const string LiteNamespace = "global::Neemle.Orm.Lite.";

    internal const string SystemNamespace = "global::System.";

    internal static CsType Lite(string name) => Cs.Type(LiteNamespace + name);

    internal static CsType Sys(string name) => Cs.Type(SystemNamespace + name);

    internal static CsType TaskType => Sys("Threading.Tasks.Task");

    internal static CsType TaskOf(CsType result) => Cs.Generic(SystemNamespace + "Threading.Tasks.Task", result);

    internal static CsType Connection => Sys("Data.Common.DbConnection");

    internal static CsType Reader => Sys("Data.Common.DbDataReader");

    internal static CsType Context => Lite("OrmContext");

    internal static CsExpression Str(string value) => Cs.Literal(value);

    internal static CsExpression Flag(bool value) => Cs.Literal(value);

    internal static CsExpression Null => Cs.Literal(null!);

    internal static CsExpression[] Strs(IEnumerable<string> values) => values.Select(Str).ToArray();

    internal static CsExpression OfTypes(this CsExpression expression, params CsType[] types) =>
        expression with { TypeArguments = new Values<CsType>(types) };

    internal static CsExpression Dot(this CsExpression expression, string member) => expression.Member(member);

    internal static CsExpression Of(this CsExpression expression, params CsExpression[] arguments) =>
        expression.Call(arguments);

    internal static CsExpression Static(string type, string member) => Cs.Id(type).Member(member);

    internal static CsStatement GiveAwaited(CsExpression expression) => Cs.Return(Cs.Await(expression));

    internal static CsSymbol Sym(string name) => Cs.Symbol(name);

    internal static CsSymbol SymMember(CsNode target, string name) => Cs.Symbol(name, new() { Target = target });

    internal static CsSymbol SymCall(CsNode target, string name, params CsNode[] arguments) =>
        Cs.Symbol(name, new() { Target = target, AsCall = true, Arguments = Cs.Compound(arguments) });

    internal static CsSymbol SymGeneric(CsNode target, string name, CsType[] types, params CsNode[] arguments) =>
        Cs.Symbol(new()
        {
            AsCall = true,
            Target = Cs.Symbol(name, new() { Target = target, TypeArguments = new Values<CsType>(types) }),
            Arguments = Cs.Compound(arguments),
        });

    internal static CsSymbol SymNew(CsType type, params CsNode[] arguments) =>
        Cs.Symbol(new() { Type = type, AsNew = true, Arguments = Cs.Compound(arguments) });

    internal static CsSymbol SymVar(string name, CsNode value) =>
        Cs.Symbol(name, new() { AsVar = true, Value = value });

    internal static CsSymbol SymAssign(CsNode target, CsNode value) =>
        Cs.Symbol(new() { AsAssignment = true, Target = target, Value = value });

    internal static CsSymbol SymChoose(CsNode condition, CsNode whenTrue, CsNode whenFalse) =>
        Cs.Symbol(new() { AsConditional = true, Parts = new Values<CsNode>(new[] { condition, whenTrue, whenFalse }) });

    internal static CsSymbol SymNull => Cs.Value(null!);

    internal static CsSymbol SymValue(object value) => Cs.Value(value);

    internal static CsInstruction SymReturn(CsNode value) =>
        Cs.Instruction(InstructionKind.Return, new() { Value = value });

    internal static CsInstruction SymThrow(CsNode value) =>
        Cs.Instruction(InstructionKind.Throw, new() { Value = value });
}
