using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.GraphSave;

internal static class SaveNavigationEmitter
{
    internal static IEnumerable<SaveNavigation> All(Entity entity, ImmutableArray<Entity> entities) =>
        References(entity, entities).Concat(Collections(entity, entities)).Concat(Links(entity, entities));

    private static IEnumerable<SaveNavigation> References(Entity entity, ImmutableArray<Entity> entities)
    {
        foreach (var reference in entity.Relations)
        {
            var target = Target(entities, reference.TargetFull);
            if (target is not null && entity.SaveSafety!.SafeReferences.Contains(reference.Nav))
                yield return Make(entity, target, (reference.Nav, "Reference", "Source", reference.FkProp));
        }
    }

    private static IEnumerable<SaveNavigation> Collections(Entity entity, ImmutableArray<Entity> entities)
    {
        foreach (var collection in entity.Collections)
        {
            var target = Target(entities, collection.ChildFull);
            if (target is not null && entity.SaveSafety!.SafeLists.Contains(collection.Nav))
                yield return Make(entity, target, (collection.Nav, "Collection", "Target", collection.InverseFk));
        }
    }

    private static IEnumerable<SaveNavigation> Links(Entity entity, ImmutableArray<Entity> entities)
    {
        foreach (var link in entity.ManyToMany)
        {
            var target = Target(entities, link.OtherFull);
            if (target is not null && entity.SaveSafety!.SafeLists.Contains(link.Nav)
                && OrmGenerator.FirstKey(entity)!.Type == "Uuid" && OrmGenerator.FirstKey(target)!.Type == "Uuid")
                yield return Make(entity, target, (link.Nav, "Link", "None", null));
        }
    }

    private static Entity? Target(ImmutableArray<Entity> entities, string full) =>
        entities.FirstOrDefault(e => e.Full == full && SaveBindingEmitter.Supported(e));

    private static SaveNavigation Make(Entity source, Entity target,
        (string Name, string Kind, string Owner, string? ForeignKey) relation)
    {
        var (name, kind, owner, foreignKey) = relation;
        var runtime = SaveBindingEmitter.Runtime;
        var shape = "new " + runtime + "GraphSaveRelationShape(" + SaveBindingEmitter.Text(name) + ", "
            + runtime + "GraphSaveRelationKind." + kind + ", " + runtime + "GraphSaveForeignKeyOwner." + owner
            + ", " + (foreignKey is null ? "null" : SaveBindingEmitter.Text(foreignKey)) + ")";
        var targets = kind == "Reference"
            ? "static e => e.@" + name + " is { } target ? new " + target.Full + "[] { target } : "
                + "global::System.Array.Empty<" + target.Full + ">()"
            : "static e => e.@" + name + " ?? new global::System.Collections.Generic.List<" + target.Full + ">()";
        return new SaveNavigation(name, target.Full, "new " + runtime + "GraphSaveRelationBinding<"
            + source.Full + ", " + target.Full + ">(" + shape + ", " + targets + ")");
    }
}
