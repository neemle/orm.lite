using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Builders;
using Neemle.CSharpComposer.Model;
using Neemle.CSharpComposer.Rendering;
using EmitScope = Neemle.OrmLite.Generator.OrmGenerator.EmitScope;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Emits the static Orm class of one entity: the table name, the descriptor, the row mapping, the query entry point
/// and, for a table, the insert, update, delete and get members. A view gets members that throw instead of writes.
/// The loaders of navigations, collections and links are emitted by the LoaderEmitter.
/// </summary>
internal static class EntityClassEmitter
{
    internal static FileBuilder EntityClass(FileBuilder file, Entity e, EmitScope scope, ImmutableArray<Entity> entities)
    {
        var entity = Cs.Type(e.Full);
        var key = OrmGenerator.KeyedApiWithheld(e) ? null : OrmGenerator.FirstKey(e);
        var cls = EntityEmitter.Access(file.Class(e.Type + "Orm"), e.Access).Static();
        cls = ReadMembers(cls, e, entity, scope.AllTables);
        cls = GraphEmitter.Members(cls, e, entities, scope);
        if (e.IsView) return ReadOnly(cls, e).End();

        cls = WriteMembers(cls, e, entity, key);
        cls = LoaderEmitter.Loaders(cls, e, entity, key, scope);
        return cls.End();
    }

    private static TypeBuilder<FileBuilder> ReadMembers(
        TypeBuilder<FileBuilder> cls, Entity e, CsType entity, HashSet<string> allTables)
    {
        var descriptor = Cs.Id("Descriptor");
        cls = cls.Field(Cs.Type("string"), "Table").Public().Const().Initialize(Syn.Str(e.Table)).End();
        cls = cls.Field(Syn.Lite("EntityDescriptor"), "Descriptor").Public().Static().ReadOnly()
            .Initialize(DescriptorEmitter.Describe(e, e.Full, allTables)).End();
        cls = cls.Method(entity, "Map").Public().Static().Parameter(Syn.Reader, "r")
            .Body(MapEmitter.MapBody(e, entity, OrmGenerator.FirstKey(e))).End();
        if (!e.IsView)
            cls = cls.Method(Cs.Type("object").Nullable().Array(), "Values").Public().Static().Parameter(entity, "e")
                .Expression(Syn.Static("OrmExec", "Row").Of(e.Props.Select(p => Cs.Id("e").Dot(p.Name)).ToArray()))
                .End();
        cls = cls.Method(Cs.Type("string"), "Column").Public().Static().Parameter(Cs.Type("string"), "prop")
            .Expression(descriptor.Dot("ColumnOf").Of(Cs.Id("prop"))).End();
        var query = Cs.Generic(Syn.LiteNamespace + "Query", entity);
        return cls.Method(query, "Query").Public().Static().Parameter(Syn.Context, "db")
            .Body(Cs.Tree(Syn.SymReturn(Syn.SymNew(query, Syn.Sym("db"), Syn.Sym("Map"))))).End();
    }

    private static TypeBuilder<FileBuilder> WriteMembers(
        TypeBuilder<FileBuilder> cls, Entity e, CsType entity, Prop? key)
    {
        var hasSet = key is not null && e.Props.Any(p => !p.IsKey && p.Name != e.SoftDeleteColumn);
        cls = Write(cls, "InsertAsync", entity, false);
        if (hasSet) cls = Write(cls, "UpdateAsync", entity, false);
        if (key is not null) cls = KeyedDelete(cls, key, false);
        cls = Write(cls, "InsertAsync", entity, true);
        if (key is null) return cls;
        cls = KeyedGet(cls, entity, key, false);
        cls = KeyedGet(cls, entity, key, true);
        if (hasSet) cls = Write(cls, "UpdateAsync", entity, true);
        return KeyedDelete(cls, key, true);
    }

    private static TypeBuilder<FileBuilder> ReadOnly(TypeBuilder<FileBuilder> cls, Entity e)
    {
        var message = e.Table + " is a view; a view is read-only, so no insert, update or delete can be built on it.";
        foreach (var name in new[] { "InsertAsync", "UpdateAsync", "DeleteAsync" })
            cls = cls.Method(Syn.TaskType, name).Public().Static()
                .Attribute(Cs.Attribute(Syn.Sys("ObsoleteAttribute"), Cs.Arg(Syn.Str(message)), Cs.Arg(Syn.Flag(true))))
                .Parameter(Cs.Type("object").Nullable().Array(), "arguments", "params")
                .Body(Cs.Tree(Syn.SymThrow(Syn.SymNew(Syn.Sys("NotSupportedException"), Syn.SymValue(message))))).End();
        return cls;
    }

    private static TypeBuilder<FileBuilder> Write(
        TypeBuilder<FileBuilder> cls, string name, CsType entity, bool withConnection)
    {
        var args = new List<CsExpression> { Cs.Id("db") };
        if (withConnection) args.Add(Cs.Id("conn"));
        args.Add(Cs.Id("Descriptor"));
        args.Add(Cs.Id("Values").Of(Cs.Id("e")));
        var method = cls.Method(Syn.TaskType, name).Public().Static().Async().Parameter(Syn.Context, "db");
        if (withConnection) method = method.Parameter(Syn.Connection, "conn");
        return method.Parameter(entity, "e")
            .Body(Cs.Statement(Cs.Await(Syn.Static("OrmExec", name).Of(args.ToArray())))).End();
    }

    private static TypeBuilder<FileBuilder> KeyedDelete(TypeBuilder<FileBuilder> cls, Prop key, bool withConnection)
    {
        var args = new List<CsExpression> { Cs.Id("db") };
        if (withConnection) args.Add(Cs.Id("conn"));
        args.Add(Cs.Id("Descriptor"));
        args.Add(Cs.Id("key"));
        var method = cls.Method(Syn.TaskOf(Cs.Int), "DeleteAsync").Public().Static().Async()
            .Parameter(Syn.Context, "db");
        if (withConnection) method = method.Parameter(Syn.Connection, "conn");
        return method.Parameter(Cs.Type(key.Cs), "key")
            .Body(Syn.GiveAwaited(Syn.Static("OrmExec", "DeleteAsync").Of(args.ToArray()))).End();
    }

    private static TypeBuilder<FileBuilder> KeyedGet(
        TypeBuilder<FileBuilder> cls, CsType entity, Prop key, bool withConnection)
    {
        var args = new List<CsExpression> { Cs.Id("db") };
        if (withConnection) args.Add(Cs.Id("conn"));
        args.Add(Cs.Id("Descriptor"));
        args.Add(Cs.Id("key"));
        args.Add(Cs.Id("Map"));
        var method = cls.Method(Syn.TaskOf(entity.Nullable()), "GetAsync").Public().Static().Async()
            .Parameter(Syn.Context, "db");
        if (withConnection) method = method.Parameter(Syn.Connection, "conn");
        return method.Parameter(Cs.Type(key.Cs), "key")
            .Body(Syn.GiveAwaited(Syn.Static("OrmExec", "GetAsync").Of(args.ToArray()))).End();
    }
}
