using Microsoft.CodeAnalysis.CSharp;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.Orphans;

internal static class OrphanBindingSource
{
    internal const string Runtime = "global::Neemle.OrmLite.";

    internal static string Factory(Entity entity, string method)
    {
        var key = OrmGenerator.FirstKey(entity)!;
        return "private static " + Runtime + "OrphanEntityBinding<" + entity.Full + ", " + key.Cs + "> "
            + method + "() => new(" + entity.Type + "Orm.Descriptor, " + entity.Type
            + "Orm.Values, static e => e.@" + key.Name + ");";
    }

    internal static string EntityRegistration(string method) =>
        Runtime + "OrphanModel.RegisterEntity(" + method + "());";

    internal static string Collection(OrphanCollectionModel collection, string parent, string child) =>
        Runtime + "OrphanModel.RegisterCollection(" + parent + "(), " + child + "(), new "
            + Runtime + "OrphanCollectionShape(" + Text(collection.Navigation) + ", "
            + Text(collection.OwnerProperty) + "));";

    private static string Text(string value) => SymbolDisplay.FormatLiteral(value, true);
}
