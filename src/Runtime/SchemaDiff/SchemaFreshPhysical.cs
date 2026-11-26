namespace Neemle.OrmLite;

internal static class SchemaFreshPhysical
{
    internal static bool Same(SchemaFixtureState state, SchemaDiffObservation actual)
    {
        var expected = state.Plan.Artifact.Recipe.After;
        var actualTables = Canonical(actual); var expectedTables = Canonical(expected);
        return SchemaObservationDigest.Physical(expected.Identity, expected.Target, actualTables)
            == SchemaObservationDigest.Physical(expected.Identity, expected.Target, expectedTables);
    }

    internal static void Validate(SchemaDiffObservation observation) => _ = Canonical(observation);

    private static IReadOnlyList<SchemaObservedTable> Canonical(SchemaDiffObservation observation)
    {
        return observation.Tables.Select(table => Canonical(table,
            observation.Target.Desired.Single(t => t.Name == table.Name), observation.ProviderProfile)).ToArray();
    }

    private static SchemaObservedTable Canonical(SchemaObservedTable table, SchemaDesiredTable desired, string profile)
    {
        var expected = SchemaCreatedInventory.Facts(desired, profile);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var column in desired.Columns.Where(_ => profile == "postgres:schema-diff-v1"))
        {
            if (!column.Nullable || column.Key)
                Pair(names, table.Facts.Constraints.GetKnown(), desired.Name, column, "nn");
            if (column.Key) Pair(names, table.Facts.Constraints.GetKnown(), desired.Name, column, "pk");
        }
        var facts = new SchemaObservedTableFacts(Inventory(table.Facts.Indexes, names),
            Inventory(table.Facts.Constraints, names), table.Facts.Triggers,
             Inventory(table.Facts.DependentObjects, names));
        if (!Equal(facts.Indexes, expected.Indexes) || !Equal(facts.Constraints, expected.Constraints)
            || !Equal(facts.Triggers, expected.Triggers) || !Equal(facts.DependentObjects, expected.DependentObjects))
            throw new SchemaDiffException(SchemaDiffError.FreshLineageUnsupported);
        return table with { Facts = facts };
    }

    private static void Pair(Dictionary<string, string> names, IReadOnlyList<SchemaObjectStamp> actual,
        string table, ColumnDescriptor column, string role)
    {
        var expected = SchemaLocalRoles.Constraint(table, column, role);
        var legacy = role == "pk" ? table + "_pkey" : table + "_" + column.Column + "_not_null";
        var matches = actual.Where(s => s.Kind == expected.Kind && s.DefinitionDigest == expected.DefinitionDigest
            && (s.Name == expected.Name || s.Name == legacy)).ToArray();
        if (matches.Length != 1) throw new SchemaDiffException(SchemaDiffError.FreshLineageUnsupported);
        names.Add(matches[0].Name, expected.Name);
    }

    private static SchemaFact<IReadOnlyList<SchemaObjectStamp>> Inventory(
        SchemaFact<IReadOnlyList<SchemaObjectStamp>> fact, IReadOnlyDictionary<string, string> names) =>
        SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known(fact.GetKnown().Select(s =>
            names.TryGetValue(s.Name, out var replacement) ? s with { Name = replacement } : s)
            .OrderBy(s => s.Kind, StringComparer.Ordinal).ThenBy(s => s.Name, StringComparer.Ordinal).ToArray());

    private static bool Equal(SchemaFact<IReadOnlyList<SchemaObjectStamp>> left,
        SchemaFact<IReadOnlyList<SchemaObjectStamp>> right) => left.GetKnown().OrderBy(s => s.Kind,
             StringComparer.Ordinal)
        .ThenBy(s => s.Name, StringComparer.Ordinal).SequenceEqual(right.GetKnown().OrderBy(s => s.Kind,
            StringComparer.Ordinal).ThenBy(s => s.Name, StringComparer.Ordinal));
}
