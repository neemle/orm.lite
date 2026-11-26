using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.PolicyCode;

namespace Neemle.OrmLite.Generator;

internal static class PolicyLoaderEmitter
{
    internal static IEnumerable<string> Members(Entity entity, HashSet<string> existing,
        ImmutableArray<Entity> entities)
    {
        foreach (var relation in entity.Relations.Where(r => existing.Contains("Load" + r.Nav + "Async")
            && entities.Any(e => e.Full == r.TargetFull && GraphEmitter.Supported(e))))
        {
            var key = "e." + Identifier(relation.FkProp);
            var value = relation.FkNullable && relation.FkValue ? key + ".Value" : key;
            var call = relation.TargetType + "Orm.GetAsync(db, conn, " + value + ", options, ct)";
            var assign = "e." + Identifier(relation.Nav) + " = await " + call + ";";
            if (relation.FkNullable) assign = "if (" + key + " is null) e." + Identifier(relation.Nav)
                + " = null; else " + assign;
            yield return Header(entity, relation.Nav, true) + " { ct.ThrowIfCancellationRequested(); " + assign + " }";
        }
        foreach (var collection in entity.Collections.Where(c => existing.Contains("Load" + c.Nav + "Async")
            && entities.Any(e => e.Full == c.ChildFull && GraphEmitter.Supported(e))))
            yield return Header(entity, collection.Nav, false) + " { e." + Identifier(collection.Nav) + " = await "
                + Runtime + "RelationQueryExtensions.ToListAsync(" + collection.ChildType + "Orm.Query(db).Where(c => c."
                + Identifier(collection.InverseFk) + " == e." + Identifier(collection.ParentKey) + "), options, ct); }";
        foreach (var link in entity.ManyToMany.Where(m => existing.Contains("Load" + m.Nav + "Async")
            && entities.Any(e => e.Full == m.OtherFull && GraphEmitter.Supported(e))))
            yield return Linked(entity, link);
    }

    private static string Header(Entity entity, string navigation, bool connection) =>
        "public static async " + Task + " Load" + navigation + "Async(" + Runtime + "OrmContext db, "
        + (connection ? "global::System.Data.Common.DbConnection conn, " : "") + entity.Full + " e, " + Options + ")";

    private static string Linked(Entity entity, OrmGenerator.ManyToMany link)
    {
        var key = OrmGenerator.FirstKey(entity)!;
        var read = "new " + Runtime + "LinkedRead<" + link.OtherFull + ">(Descriptor, " + Literal(link.Nav)
            + ", e." + Identifier(key.Name) + ", " + link.OtherType + "Orm.Descriptor, " + link.OtherType + "Orm.Map)";
        return Header(entity, link.Nav, true) + " { e." + Identifier(link.Nav) + " = await " + Runtime
            + "OrmExec.LoadLinkedAsync(db, conn, " + read + ", options, ct); }";
    }
}
