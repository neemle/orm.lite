namespace Neemle.OrmLite.Tests.Corpus;

// The approved deviations that the provider-independent migration identity (ORML-21 slice S5, owner decision D1)
// moves in the corpus goldens: the index-set hash and the history set hash, and every version name built from them,
// are now computed from the declarations alone. The old texts are read from the recorded goldens and the new texts
// are derived from the structural formulas, not captured: the capture regenerates the goldens and INDEX.md, and this
// list only documents what moved and why.
internal static class CorpusIdentityDeviations
{
    private const string Reason =
        "ORML-21 S5 (owner decision D1): the index-set hash was the hash of the PostgreSQL-rendered create statements "
        + "and the history set hash the hash of the PostgreSQL-rendered install, whenever the PostgreSQL provider "
        + "was loaded, and a structural hash otherwise, so the 9900_ version names depended on what was loaded. "
        + "Both are now the structural hash only, in the core: the index set over table, index name, uniqueness, live "
        + "column, columns and shape, the history set over the table names and their argument hashes and, when it "
        + "is not the library default, the actor setting name (the corpus runs under the default). A database "
        + "that carries the old names runs the renamed reconcile and history installs once (idempotent) and records "
        + "one new schema_migrations row for each. Nothing else on the line moves.";

    private const string Executed = "executed-schema-contributions-and-registry";

    private const string Transcripts =
        "migration-runner-fresh-database, migration-runner-partially-applied and " + Executed;

    private sealed record Hashes(string Before, string After);

    private sealed record Moved(string Set, string Token, string Goldens, Hashes Index, Hashes History);

    private static Moved Of(string set, string token, string goldens, Hashes index, Hashes history) =>
        new(set, token, goldens, index, history);

    private static readonly Moved[] Sets =
    [
        Of("generator-demo", "OrmLiteDemo", Transcripts,
            new("d27e299df0226960", "a7104bccd4193080"), new("e9dbcb569c4fbc15", "6df7f955b08f5c73")),
        Of("generator-corpus-entities", "CorpusEntities", Transcripts,
            new("e3aab199192c293c", "e5ff1a2ec4afdb56"), new("75a913ef24f4eca1", "e61f568d5478fd36")),
        Of("generator-golden-fixtures", "OrmLiteGoldenFixtures", Transcripts,
            new("eb6e32bc81a12870", "cf6209a70c2a593c"), new("", "")),
        Of("generator-global-namespace", "CorpusGlobalNamespace", Transcripts,
            new("cdf8e7986621fc5e", "ae551789d8c9818c"), new("0aa8a0d2f203193f", "1a3c33d971c4c2ce")),
        Of("generator-tenant-option-default", "TenantOptionDefault", Executed,
            new("cbf29ce484222325", "cbf29ce484222325"), new("58a560a7fa6d2d7f", "99ac1720f8990e2b")),
        Of("generator-tenant-option-invalid", "TenantOptionInvalid", Executed,
            new("cbf29ce484222325", "cbf29ce484222325"), new("58a560a7fa6d2d7f", "99ac1720f8990e2b")),
        Of("generator-tenant-option-scope-id", "TenantOptionScope", Executed,
            new("cbf29ce484222325", "cbf29ce484222325"), new("4ebf33eaebd07b21", "b26c64fa058bdef5")),
    ];

    private static string Identity(Moved moved, bool before)
    {
        var index = before ? moved.Index.Before : moved.Index.After;
        var history = before ? moved.History.Before : moved.History.After;
        var parts = new List<string>();
        if (moved.Index.Before != moved.Index.After)
        {
            parts.Add("9900_orm_index_reconcile_" + moved.Token + "_" + index);
            parts.Add("IndexSetHash: " + index);
        }
        if (history.Length > 0)
        {
            parts.Add("9900_orm_history_reconcile_" + moved.Token + "_" + history);
            parts.Add("HistorySetHash: " + history);
        }
        return string.Join("; ", parts);
    }

    private static CorpusIndex.Deviation OfSet(Moved moved) =>
        new(
            moved.Set + "/" + moved.Goldens + " (the 9900_ version names and the set hashes)",
            CorpusIndex.DeviationKind.Changed,
            Identity(moved, true),
            Identity(moved, false),
            Reason);

    private static CorpusIndex.Deviation SetHashVectors() =>
        new(
            "hash/set-hash-vectors",
            CorpusIndex.DeviationKind.Changed,
            "no tables = 55b686441ec82b88; tenant table = e4071fdc5c0927f5; platform table = 28d441b68fac0005; "
            + "tenant then platform = 3525748bf40af974; platform then tenant = 7806d68ad12060f7; "
            + "version name formula = 9900_orm_history_reconcile_Assembly_e4071fdc5c0927f5",
            "no tables = e3b0c44298fc1c14; tenant table = 991e2994cc4b646b; platform table = 30a6a6da8c76b2a8; "
            + "tenant then platform = f8c47a43002b5b36; platform then tenant = b6f6731be62a61a9; "
            + "version name formula = 9900_orm_history_reconcile_Assembly_991e2994cc4b646b",
            Reason);

    private static CorpusIndex.Deviation SetHashEqualsReconcile() =>
        new(
            "hash/set-hash-equals-statements-hash-of-reconcile",
            CorpusIndex.DeviationKind.Changed,
            "e4071fdc5c0927f5 == e4071fdc5c0927f5 : True",
            "991e2994cc4b646b == e4071fdc5c0927f5 : False",
            Reason + " The hash of the rendered reconcile statements, StatementsHash, is unchanged; the set hash no "
            + "longer equals it, which is the point of the change.");

    internal static readonly CorpusIndex.Deviation[] All =
    [
        SetHashVectors(),
        SetHashEqualsReconcile(),
        .. Sets.Select(OfSet),
    ];
}
