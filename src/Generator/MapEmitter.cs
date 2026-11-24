using System.Collections.Generic;
using System.Linq;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Model;
using Neemle.CSharpComposer.Rendering;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Emits the Map method of an entity, which turns a data reader row into an instance: the ordinal of each column,
/// the construction with the init-only members, and the assignment of each other member. An entity that cannot be
/// built gets a Map that throws instead.
/// </summary>
internal static class MapEmitter
{
    private static readonly Dictionary<string, string> Getters = new()
    {
        ["Int32"] = "GetInt32",
        ["Int64"] = "GetInt64",
        ["Boolean"] = "GetBoolean",
        ["Double"] = "GetDouble",
        ["Decimal"] = "GetDecimal",
        ["DateTime"] = "GetDateTime",
        ["Guid"] = "GetGuid",
    };

    internal static CsTree MapBody(Entity e, CsType entity, Prop? key)
    {
        if (Buildability.IsUnbuildable(e)) return UnbuildableBody(e);
        var keyColumn = key?.Column ?? "";
        var initialised = e.Props.Any(p => p.InInit);
        var body = new List<CsNode>();
        if (!initialised) body.Add(Syn.SymVar("e", Syn.SymNew(entity)));
        for (var i = 0; i < e.Props.Length; i++)
            body.Add(Syn.SymVar("o" + i, Syn.SymCall(Syn.Sym("r"), "GetOrdinal", Syn.SymValue(e.Props[i].Column))));
        if (initialised)
            body.Add(Syn.SymVar("e", ConstructedWith(entity, AssignmentsOf(e, keyColumn, true))));
        body.AddRange(AssignmentsOf(e, keyColumn, false));
        body.Add(Syn.SymReturn(Syn.Sym("e")));
        return Cs.Tree(body.ToArray());
    }

    private static CsTree UnbuildableBody(Entity e) =>
        Cs.Tree(Syn.SymThrow(Syn.SymNew(Syn.Sys("NotSupportedException"), Syn.SymValue(Buildability.ThrowText(e)))));

    private static CsSymbol ConstructedWith(CsType entity, IEnumerable<CsNode> assignments) =>
        Cs.Symbol(new()
        {
            Type = entity,
            AsNew = true,
            Arguments = Cs.Compound(),
            Initializer = Cs.Symbol(new() { AsInitializer = true, Parts = new Values<CsNode>(assignments.ToArray()) }),
        });

    private static IEnumerable<CsNode> AssignmentsOf(Entity e, string keyColumn, bool init)
    {
        for (var i = 0; i < e.Props.Length; i++)
        {
            var p = e.Props[i];
            if (p.InInit != init) continue;
            var ordinal = Syn.Sym("o" + i);
            var read = ReadOf(p, ordinal, e.Table, keyColumn);
            CsNode value = p.Nullable
                ? Syn.SymChoose(Syn.SymCall(Syn.Sym("r"), "IsDBNull", ordinal), Syn.SymNull, read)
                : read;
            yield return Syn.SymAssign(init ? Syn.Sym(p.Name) : Syn.SymMember(Syn.Sym("e"), p.Name), value);
        }
    }

    private static CsNode ReadOf(Prop p, CsNode ordinal, string table, string keyColumn)
    {
        var reader = Syn.Sym("r");
        if (Getters.TryGetValue(p.Read, out var getter)) return Syn.SymCall(reader, getter, ordinal);
        return p.Read switch
        {
            "Date" => Syn.SymGeneric(reader, "GetFieldValue", [Syn.Sys("DateOnly")], ordinal),
            "Enum" => Syn.SymGeneric(
                Syn.Sym("OrmEnum"), "Read", [Cs.Type(p.Cs)],
                reader, ordinal, Syn.SymValue(table), Syn.SymValue(p.Column), Syn.SymValue(keyColumn)),
            "Jsonb" => Syn.SymCall(Syn.Sym("Jsonb"), "FromStorage", Syn.SymCall(reader, "GetString", ordinal)),
            _ => Syn.SymCall(reader, "GetString", ordinal),
        };
    }
}
