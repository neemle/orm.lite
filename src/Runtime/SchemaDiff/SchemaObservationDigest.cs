using System.Globalization;

namespace Neemle.OrmLite;

internal static class SchemaObservationDigest
{
    internal static string Physical(SchemaObservationIdentity identity, SchemaDiffSnapshot target,
        IReadOnlyList<SchemaObservedTable> tables)
    {
        var w = new SchemaDiffCanonical();
        w.Add("schema-physical-v1"); w.Add(identity.DatabaseIdentityDigest); w.Add(identity.ProviderProfile);
        w.Add(identity.Consistent.ToString()); w.Add(target.Scope.DatabaseScopeId); w.Add(target.Scope.Namespace);
        foreach (var table in tables)
        {
            w.Add(table.Name); w.Fact(table.Exists, v => w.Add(v.ToString()));
            foreach (var column in table.Columns) Column(w, column);
            Inventory(w, table.Facts.Indexes); Inventory(w, table.Facts.Constraints);
            Inventory(w, table.Facts.Triggers); Inventory(w, table.Facts.DependentObjects);
        }
        return w.Digest();
    }

    internal static string History(SchemaObservationHistory history)
    {
        var w = new SchemaDiffCanonical(); w.Add("schema-history-v1");
        w.Fact(history.HistoryTableExists, v => w.Add(v.ToString()));
        w.Fact(history.AppliedVersions, values => { foreach (var value in values) w.Add(value); });
        return w.Digest();
    }

    private static void Column(SchemaDiffCanonical w, SchemaObservedColumn column)
    {
        w.Add(column.Name);
        w.Fact(column.Type, v =>
        {
            w.Add(v.CanonicalStorage); w.Add(v.Precision?.ToString(CultureInfo.InvariantCulture));
            w.Add(v.Scale?.ToString(CultureInfo.InvariantCulture));
        });
        w.Fact(column.Nullable, v => w.Add(v.ToString()));
        w.Fact(column.KeyPosition, v => w.Add(v.ToString(CultureInfo.InvariantCulture)));
        w.Fact(column.Facts.Default, v => w.Add(v.Kind.ToString()));
        w.Fact(column.Facts.Generated, v => w.Add(v.ToString()));
        w.Fact(column.Facts.Identity, v => w.Add(v.ToString()));
        w.Fact(column.Facts.Collation, w.Add);
    }

    private static void Inventory(SchemaDiffCanonical w, SchemaFact<IReadOnlyList<SchemaObjectStamp>> fact) =>
        w.Fact(fact, values =>
        {
            w.Add(values.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var value in values) { w.Add(value.Kind); w.Add(value.Name); w.Add(value.DefinitionDigest); }
        });
}
