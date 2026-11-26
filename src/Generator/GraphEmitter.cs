using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Builders;
using Neemle.CSharpComposer.Model;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using Relation = Neemle.OrmLite.Generator.OrmGenerator.Relation;
using Collection = Neemle.OrmLite.Generator.OrmGenerator.Collection;
using ManyToMany = Neemle.OrmLite.Generator.OrmGenerator.ManyToMany;
using EmitScope = Neemle.OrmLite.Generator.OrmGenerator.EmitScope;

namespace Neemle.OrmLite.Generator;

internal static class GraphEmitter
{
    internal static bool Supported(Entity entity) => !entity.IsView && !OrmGenerator.KeyedApiWithheld(entity)
        && !Buildability.IsUnbuildable(entity) && OrmGenerator.FirstKey(entity) is { Nullable: false };

    internal static TypeBuilder<FileBuilder> Members(TypeBuilder<FileBuilder> cls, Entity entity,
        ImmutableArray<Entity> entities, EmitScope scope)
    {
        if (!Supported(entity)) return cls;
        var key = OrmGenerator.FirstKey(entity)!;
        var navigations = Navigations(entity, entities, scope).ToArray();
        var binding = New("GraphEntityBinding", [Cs.Type(entity.Full)],
            [Cs.Id("Descriptor"), Cs.Id("Map"), Key(entity, key), .. navigations.Select(n => n.Binding)]);
        cls = cls.Method(Cs.Void, "RegisterGraph").Internal().Static()
            .Body(Cs.Statement(Syn.Static("GraphModel", "Register").OfTypes(Cs.Type(entity.Full)).Of(binding))).End();
        var paths = cls.Class("Paths").Public().Static();
        foreach (var nav in navigations)
        {
            var type = Cs.Generic(Syn.LiteNamespace + "GraphPath", Cs.Type(entity.Full), Cs.Type(nav.Target));
            paths = paths.Property(type, nav.Name).Public().Static().Expression(
                Syn.Static("GraphPaths", "Edge").OfTypes(Cs.Type(entity.Full), Cs.Type(nav.Target))
                    .Of(Syn.Str(nav.Name))).End();
        }
        return paths.End();
    }

    private static IEnumerable<(string Name, string Target, CsExpression Binding)> Navigations(
        Entity entity, ImmutableArray<Entity> entities, EmitScope scope)
    {
        foreach (var relation in entity.Relations)
        {
            var target = entities.FirstOrDefault(e => e.Full == relation.TargetFull);
            if (Available(entity, target, relation.Nav, scope))
                yield return (relation.Nav, target!.Full, Reference(entity, target, relation));
        }
        foreach (var collection in entity.Collections)
        {
            var target = entities.FirstOrDefault(e => e.Full == collection.ChildFull);
            if (Available(entity, target, collection.Nav, scope)
                && target!.Props.Any(p => p.Name == collection.InverseFk))
                yield return (collection.Nav, target.Full, CollectionBinding(entity, target, collection));
        }
        foreach (var link in entity.ManyToMany)
        {
            var target = entities.FirstOrDefault(e => e.Full == link.OtherFull);
            if (Available(entity, target, link.Nav, scope))
                yield return (link.Nav, target!.Full, Link(entity, target, link));
        }
    }

    private static bool Available(Entity source, Entity? target, string navigation, EmitScope scope) =>
        target is not null && Supported(target) && !scope.Guard.Blocked.Contains((source.Full, navigation));

    private static CsExpression Key(Entity entity, Prop key) => New("GraphKeyBinding",
        [Cs.Type(entity.Full), Cs.Type(key.Cs)], [Syn.Str(key.Column), Read("e", key.Name)]);

    private static CsExpression Reference(Entity source, Entity target, Relation relation)
    {
        var fk = source.Props.Single(p => p.Name == relation.FkProp);
        var key = OrmGenerator.FirstKey(target)!;
        var value = Cs.Id("e").Dot(fk.Name);
        var present = relation.FkNullable ? Cs.Binary(CsOperator.NotEqual, value, Syn.Null) : Syn.Flag(true);
        var read = relation.FkValue ? value.Dot("GetValueOrDefault").Of() : value;
        if (relation.FkNullable && !relation.FkValue)
            read = Cs.SwitchExpression(value, Cs.Arm(Cs.ConstantPattern(Syn.Null), Syn.Str("")),
                Cs.Arm(Cs.VarPattern("key"), Cs.Id("key")));
        var graphKey = New("GraphKey", [Cs.Type(key.Cs)], [present, read]);
        return New("GraphReferenceBinding", [Cs.Type(source.Full), Cs.Type(target.Full), Cs.Type(key.Cs)],
            [Syn.Str(relation.Nav), Syn.Str(fk.Column), Cs.Lambda(graphKey, Cs.LambdaParameter("e")),
                Assign(relation.Nav)]);
    }

    private static CsExpression CollectionBinding(Entity source, Entity target, Collection collection)
    {
        var key = OrmGenerator.FirstKey(source)!;
        var inverse = target.Props.Single(p => p.Name == collection.InverseFk);
        var value = Cs.Id("e").Dot(inverse.Name);
        var present = inverse.Nullable ? Cs.Binary(CsOperator.NotEqual, value, Syn.Null) : Syn.Flag(true);
        var read = inverse.NullableValue ? value.Dot("GetValueOrDefault").Of() : value;
        if (inverse.Nullable && !inverse.NullableValue)
            read = Cs.SwitchExpression(value, Cs.Arm(Cs.ConstantPattern(Syn.Null), Syn.Str("")),
                Cs.Arm(Cs.VarPattern("key"), Cs.Id("key")));
        var graphKey = New("GraphKey", [Cs.Type(key.Cs)], [present, read]);
        return New("GraphCollectionBinding", [Cs.Type(source.Full), Cs.Type(target.Full), Cs.Type(key.Cs)],
            [Syn.Str(collection.Nav), Syn.Str(inverse.Column), Read("e", key.Name),
                Cs.Lambda(graphKey, Cs.LambdaParameter("e")), Assign(collection.Nav)]);
    }

    private static CsExpression Link(Entity source, Entity target, ManyToMany link)
    {
        var key = OrmGenerator.FirstKey(source)!;
        var targetKey = OrmGenerator.FirstKey(target)!;
        var table = New("GraphLinkSource", [],
            [Syn.Str(link.JoinTable), Syn.Str(link.ThisKey), Syn.Str(link.OtherKey)]);
        return New("GraphLinkBinding",
            [Cs.Type(source.Full), Cs.Type(target.Full), Cs.Type(key.Cs), Cs.Type(targetKey.Cs)],
            [Syn.Str(link.Nav), table, Read("e", key.Name), Assign(link.Nav)]);
    }

    private static CsExpression Read(string argument, string member) =>
        Cs.Lambda(Cs.Id(argument).Dot(member), Cs.LambdaParameter(argument));

    private static CsExpression Assign(string navigation) => Cs.Lambda(
        Cs.Assign(Cs.Id("e").Dot(navigation), Cs.Id("value")),
        Cs.LambdaParameter("e"), Cs.LambdaParameter("value"));

    private static CsExpression New(string name, CsType[] types, CsExpression[] arguments) =>
        new(ExpressionKind.New, "", new Values<CsExpression>(arguments))
        { Type = types.Length == 0 ? Syn.Lite(name) : Cs.Generic(Syn.LiteNamespace + name, types) };
}
