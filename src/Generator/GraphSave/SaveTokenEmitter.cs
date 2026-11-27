using System.Collections.Generic;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.GraphSave;

internal static class SaveTokenEmitter
{
    internal static IEnumerable<string> Members(Entity entity, SaveNavigation[] relations,
        IEnumerable<string> reservedNames)
    {
        var names = relations.Select(r => r.Name).Concat(reservedNames).ToHashSet(System.StringComparer.Ordinal);
        var container = "__GraphSaveTokens";
        while (names.Contains(container)) container += "_";
        yield return "public static " + container + " SaveRelations { get; } = new " + container + "();";
        var tokens = relations.Select(r => "public " + SaveBindingEmitter.Runtime + "SaveRelation<" + entity.Full
            + ", " + r.Target + "> @" + r.Name + " => " + SaveBindingEmitter.Runtime + "SaveRelations.Edge<"
            + entity.Full + ", " + r.Target + ">(" + SaveBindingEmitter.Text(r.Name) + ");");
        yield return "public sealed class " + container + " { internal " + container + "() { } "
            + string.Join(" ", tokens) + " }";
    }
}
