using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.GraphSave;

internal static class SaveBindingEmitter
{
    internal const string Runtime = "global::Neemle.OrmLite.";

    internal static bool Supported(Entity e) => e.Managed && !e.IsView && GraphEmitter.Supported(e)
        && e.SaveSafety is { PhysicalValuesSafe: true } && e.LoadingPolicy is null
        && OrmGenerator.FirstKey(e)!.Type != "Json" && SafeSoft(e.Soft);

    private static bool SafeSoft(OrmGenerator.SoftDeclaration? soft) => soft is null || soft.Platform
        && !soft.TenantDeclared && soft.LabelName is null && soft.OwnerColumns.IsEmpty
        && soft.PinnedColumns.IsEmpty && soft.StatusColumns.IsEmpty && soft.UnversionedColumns.IsEmpty;

    internal static IEnumerable<string> Members(Entity e, ImmutableArray<Entity> entities)
    {
        var relations = SaveNavigationEmitter.All(e, entities).ToArray();
        var key = OrmGenerator.FirstKey(e)!;
        var binding = "new " + Runtime + "GraphSaveEntityBinding<" + e.Full + ", " + key.Cs + ">("
            + "Descriptor, Values, static e => e.@" + key.Name + ", Map, new " + Runtime
            + "GraphSaveRelationBinding<" + e.Full + ">[] { "
            + string.Join(", ", relations.Select(r => r.Binding)) + " })";
        yield return "internal static void RegisterSave() { " + Runtime
            + "GraphSaveModel.Register(" + binding + "); }";
        var tokens = relations.Select(r => "public static " + Runtime + "SaveRelation<" + e.Full + ", " + r.Target
            + "> @" + r.Name + " => " + Runtime + "SaveRelations.Edge<" + e.Full + ", " + r.Target + ">(" + Text(r.Name)
            + ");");
        yield return "public static class SaveRelations { " + string.Join(" ", tokens) + " }";
    }

    internal static string Text(string value) => SymbolDisplay.FormatLiteral(value, true);
}
