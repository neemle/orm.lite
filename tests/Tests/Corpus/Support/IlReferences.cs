using System.Reflection;
using System.Reflection.Emit;

namespace Neemle.OrmLite.Tests.Corpus;

public static class IlReferences
{
    private static readonly Dictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => (ushort)o.Value);

    public static string KeyOf(MemberInfo member)
    {
        var type = member.DeclaringType!;
        if (type.IsGenericType && !type.IsGenericTypeDefinition) type = type.GetGenericTypeDefinition();
        var name = type.Name.Contains('`') ? type.Name[..type.Name.IndexOf('`')] : type.Name;
        return member switch
        {
            MethodBase method => name + "." + method.Name + "/" + method.GetParameters().Length,
            _ => name + "." + member.Name,
        };
    }

    public static HashSet<string> Scan(Assembly assembly, params string[] prefixes)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in assembly.GetTypes().Where(t => prefixes.Any(p => (t.FullName ?? "").StartsWith(p, StringComparison.Ordinal))))
        {
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
                Scan(method, keys);
        }
        return keys;
    }

    private static void Scan(MethodBase method, HashSet<string> keys)
    {
        var body = method.GetMethodBody();
        if (body is null) return;
        var il = body.GetILAsByteArray();
        if (il is null) return;
        var module = method.Module;
        var typeArguments = method.DeclaringType is { IsGenericType: true } ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var i = 0;
        while (i < il.Length)
        {
            ushort code = il[i++];
            if (code == 0xFE) code = (ushort)(0xFE00 | il[i++]);
            var op = OpCodesByValue[code];
            switch (op.OperandType)
            {
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                case OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, i);
                    try
                    {
                        var member = module.ResolveMember(token, typeArguments, methodArguments);
                        if (member is not null && member.DeclaringType is not null) keys.Add(KeyOf(member));
                    }
                    catch (ArgumentException)
                    {
                    }
                    i += 4;
                    break;
                case OperandType.InlineSwitch:
                    var count = BitConverter.ToInt32(il, i);
                    i += 4 + 4 * count;
                    break;
                default:
                    i += Size(op.OperandType);
                    break;
            }
        }
    }

    private static int Size(OperandType type) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => 4,
    };
}
