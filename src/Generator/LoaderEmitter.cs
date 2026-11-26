using System.Collections.Generic;
using System.Linq;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Builders;
using Neemle.CSharpComposer.Model;
using Neemle.CSharpComposer.Rendering;
using ChildGuard = Neemle.OrmLite.Generator.OrmGenerator.ChildGuard;
using Collection = Neemle.OrmLite.Generator.OrmGenerator.Collection;
using EmitScope = Neemle.OrmLite.Generator.OrmGenerator.EmitScope;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using ManyToMany = Neemle.OrmLite.Generator.OrmGenerator.ManyToMany;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using Relation = Neemle.OrmLite.Generator.OrmGenerator.Relation;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Emits the loaders of an entity: one for each navigation to a parent, each collection of children and each
/// many-to-many link, and the add and remove members of a link whose join table the model does not declare. A loader
/// is left out when its target is not generated, has no usable key, or lost a member.
/// </summary>
internal static class LoaderEmitter
{
    internal static TypeBuilder<FileBuilder> Loaders(
        TypeBuilder<FileBuilder> cls, Entity e, CsType entity, Prop? key, EmitScope scope)
    {
        foreach (var rel in RelationsOf(e, scope.Refused, scope.Guard)) cls = LoadRelation(cls, entity, rel);
        foreach (var col in CollectionsOf(e, scope.Guard)) cls = LoadCollection(cls, entity, col);
        foreach (var mm in LinksOf(e, scope.Refused, scope.Guard))
            cls = Linked(cls, entity, key, mm, !scope.AllTables.Contains(mm.JoinTable));
        return cls;
    }

    private static IEnumerable<Relation> RelationsOf(Entity e, HashSet<string> refused, ChildGuard guard) =>
        e.Relations.Where(r => !refused.Contains(r.TargetType) && !guard.Blocked.Contains((e.Full, r.Nav)));

    private static IEnumerable<Collection> CollectionsOf(Entity e, ChildGuard guard) =>
        e.Collections.Where(c => !OrmGenerator.KeyedApiWithheld(e) && Loadable(c, guard)
            && !guard.Blocked.Contains((e.Full, c.Nav)));

    private static IEnumerable<ManyToMany> LinksOf(Entity e, HashSet<string> refused, ChildGuard guard) =>
        e.ManyToMany.Where(m => !OrmGenerator.KeyedApiWithheld(e) && !refused.Contains(m.OtherType)
            && !guard.Blocked.Contains((e.Full, m.Nav)));

    private static bool Loadable(Collection c, ChildGuard guard) =>
        !guard.Dropped.Contains(c.ChildType) && !guard.Excluded.Contains((c.ChildType, c.InverseFk));

    private static TypeBuilder<FileBuilder> LoadRelation(TypeBuilder<FileBuilder> cls, CsType entity, Relation rel)
    {
        var target = Cs.Id(rel.TargetType + "Orm");
        var nav = Cs.Id("e").Dot(rel.Nav);
        var fk = Cs.Id("e").Dot(rel.FkProp);
        CsStatement body;
        if (rel.FkNullable)
        {
            var value = rel.FkValue ? fk.Dot("Value") : fk;
            var get = target.Dot("GetAsync").Of(Cs.Id("db"), Cs.Id("conn"), value);
            var load = Cs.Statement(Cs.Assign(nav, Cs.Await(get)));
            body = Cs.If(
                Cs.Binary(CsOperator.Equal, fk, Syn.Null),
                Cs.Body(Cs.Statement(Cs.Assign(nav, Syn.Null))),
                Cs.Block(load));
        }
        else
        {
            body = Cs.Statement(Cs.Assign(nav, Cs.Await(target.Dot("GetAsync").Of(Cs.Id("db"), Cs.Id("conn"), fk))));
        }
        return cls.Method(Syn.TaskType, "Load" + rel.Nav + "Async").Public().Static().Async()
            .Parameter(Syn.Context, "db").Parameter(Syn.Connection, "conn").Parameter(entity, "e").Body(body).End();
    }

    private static TypeBuilder<FileBuilder> LoadCollection(
        TypeBuilder<FileBuilder> cls, CsType entity, Collection col)
    {
        var match = Cs.Lambda(
            Cs.Binary(CsOperator.Equal, Cs.Id("c").Dot(col.InverseFk), Cs.Id("e").Dot(col.ParentKey)),
            Cs.LambdaParameter("c"));
        var load = Cs.Id(col.ChildType + "Orm").Dot("Query").Of(Cs.Id("db")).Dot("Where").Of(match)
            .Dot("ToListAsync").Of();
        return cls.Method(Syn.TaskType, "Load" + col.Nav + "Async").Public().Static().Async()
            .Parameter(Syn.Context, "db").Parameter(entity, "e")
            .Body(Cs.Statement(Cs.Assign(Cs.Id("e").Dot(col.Nav), Cs.Await(load)))).End();
    }

    private static TypeBuilder<FileBuilder> Linked(
        TypeBuilder<FileBuilder> cls, CsType entity, Prop? key, ManyToMany mm, bool pure)
    {
        var thisKey = Cs.Id("e").Dot(key?.Name ?? "Id");
        var target = Cs.Id(mm.OtherType + "Orm");
        var read = new CsExpression(ExpressionKind.New, "", new Values<CsExpression>([
            Cs.Id("Descriptor"), Syn.Str(mm.Nav), thisKey, target.Dot("Descriptor"), target.Dot("Map"),
        ])) { Type = Cs.Generic(Syn.LiteNamespace + "LinkedRead", Cs.Type(mm.OtherFull)) };
        var load = Syn.Static("OrmExec", "LoadLinkedAsync").Of(Cs.Id("db"), Cs.Id("conn"), read);
        cls = cls.Method(Syn.TaskType, "Load" + mm.Nav + "Async").Public().Static().Async()
            .Parameter(Syn.Context, "db").Parameter(Syn.Connection, "conn").Parameter(entity, "e")
            .Body(Cs.Statement(Cs.Assign(Cs.Id("e").Dot(mm.Nav), Cs.Await(load)))).End();
        return pure ? AddRemove(cls, key, mm) : cls;
    }

    /// <summary>The add and remove members of a link whose join table has no entity of its own.</summary>
    private static TypeBuilder<FileBuilder> AddRemove(TypeBuilder<FileBuilder> cls, Prop? key, ManyToMany mm)
    {
        var singular = Singular.Of(mm.Nav);
        var thisType = Cs.Type(key?.Cs ?? "global::System.Guid");
        var otherType = Cs.Type(mm.OtherKeyCs);
        foreach (var verb in new[] { "Add", "Remove" })
            cls = cls.Method(Syn.TaskType, verb + singular + "Async").Public().Static().Async()
                .Parameter(Syn.Context, "db").Parameter(Syn.Connection, "conn")
                .Parameter(thisType, "thisId").Parameter(otherType, "otherId")
                .Body(Cs.Statement(Cs.Await(Syn.Static("OrmExec", verb + "LinkAsync").Of(
                    Cs.Id("db"), Cs.Id("conn"), Cs.Id("Descriptor"), Syn.Str(mm.Nav), Cs.Id("thisId"),
                    Cs.Id("otherId"))))).End();
        return cls;
    }
}
