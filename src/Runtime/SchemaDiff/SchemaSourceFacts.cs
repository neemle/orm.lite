namespace Neemle.OrmLite;

internal static class SchemaSourceFacts
{
    internal static string Observation(SchemaSourceBuilder w, SchemaDiffObservation observation)
    {
        var identity = observation.Identity;
        var id = w.Call("R.SchemaObservationIdentity", "new R.SchemaObservationIdentity",
            w.Text(identity.DatabaseIdentityDigest), w.Text(identity.ProviderProfile), "true");
        var history = w.Call("R.SchemaObservationHistory", "new R.SchemaObservationHistory",
            Fact(w, "bool", SchemaSourceTarget.Boolean(observation.History.HistoryTableExists.GetKnown())),
            Fact(w, "G.IReadOnlyList<string>",
                w.List("string", observation.History.AppliedVersions.GetKnown().Select(w.Text))));
        var request = w.Call("R.SchemaDiffInspection", "new R.SchemaDiffInspection",
            SchemaSourceTarget.Write(w, observation.Target), w.Text(observation.ProviderProfile));
        return w.Call("R.SchemaDiffObservation", "R.SchemaDiffObservation.FromFacts", id,
            w.List("R.SchemaObservedTable", observation.Tables.Select(t => Table(w, t))), history, request);
    }

    private static string Table(SchemaSourceBuilder w, SchemaObservedTable table)
    {
        var facts = w.Call("R.SchemaObservedTableFacts", "new R.SchemaObservedTableFacts",
            Inventory(w, table.Facts.Indexes), Inventory(w, table.Facts.Constraints),
            Inventory(w, table.Facts.Triggers), Inventory(w, table.Facts.DependentObjects));
        return w.Call("R.SchemaObservedTable", "new R.SchemaObservedTable", w.Text(table.Name),
            Fact(w, "bool", SchemaSourceTarget.Boolean(table.Exists.GetKnown())),
            w.List("R.SchemaObservedColumn", table.Columns.Select(c => Column(w, c))), facts);
    }

    private static string Column(SchemaSourceBuilder w, SchemaObservedColumn column)
    {
        var type = column.Type.GetKnown();
        var storage = w.Call("R.SchemaStorageType", "new R.SchemaStorageType", w.Text(type.CanonicalStorage),
            type.Precision is { } precision ? SchemaSourceTarget.Number(precision) : "null",
            type.Scale is { } scale ? SchemaSourceTarget.Number(scale) : "null");
        return w.Call("R.SchemaObservedColumn", "new R.SchemaObservedColumn", w.Text(column.Name),
            Fact(w, "R.SchemaStorageType", storage),
            Fact(w, "bool", SchemaSourceTarget.Boolean(column.Nullable.GetKnown())),
            Fact(w, "int", SchemaSourceTarget.Number(column.KeyPosition.GetKnown())), ColumnFacts(w, column.Facts));
    }

    private static string ColumnFacts(SchemaSourceBuilder w, SchemaObservedColumnFacts facts) =>
        w.Call("R.SchemaObservedColumnFacts", "new R.SchemaObservedColumnFacts",
            Fact(w, "R.SchemaDefault",
                "new R.SchemaDefault(R.SchemaDefaultKind." + facts.Default.GetKnown().Kind + ")"),
            Fact(w, "bool", SchemaSourceTarget.Boolean(facts.Generated.GetKnown())),
            Fact(w, "bool", SchemaSourceTarget.Boolean(facts.Identity.GetKnown())),
            Fact(w, "string?", w.Text(facts.Collation.GetKnown())));

    private static string Inventory(SchemaSourceBuilder w, SchemaFact<IReadOnlyList<SchemaObjectStamp>> fact) =>
        Fact(w, "G.IReadOnlyList<R.SchemaObjectStamp>",
            w.List("R.SchemaObjectStamp", fact.GetKnown().Select(v => w.Call("R.SchemaObjectStamp",
                "new R.SchemaObjectStamp", w.Text(v.Kind), w.Text(v.Name), w.Text(v.DefinitionDigest)))));

    private static string Fact(SchemaSourceBuilder w, string type, string value) =>
        w.Call("R.SchemaFact<" + type + ">", "R.SchemaFact<" + type + ">.Known", value);
}
