namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class JsonCases
{
    private const string G = "json";

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var legacy = new MovedFieldKey("legacy_name");
        var other = new MovedFieldKey("other_field");
        var third = new MovedFieldKey("third_field");
        var document = Jsonb.Parse("{\"a\":1}");
        var values = new List<string> { "red", "green", "blue" };
        var single = new List<string> { "only" };

        run.Select<CorpusNote>(G, "where-jsonb-parameter-is-cast", q => q.Where(n => n.Payload == document));
        run.Select<CorpusNote>(G, "where-key-equals-text", q => q.Where(n => Db.JsonKeyEquals(n.Payload, "name", "alpha", JsonValueOrder.Text)));
        run.Select<CorpusNote>(G, "where-key-equals-number", q => q.Where(n => Db.JsonKeyEquals(n.Payload, "count", "10", JsonValueOrder.Number)));
        run.Select<CorpusNote>(G, "where-key-equals-boolean", q => q.Where(n => Db.JsonKeyEquals(n.Payload, "flag", "true", JsonValueOrder.Boolean)));
        run.Select<CorpusNote>(G, "where-has-key", q => q.Where(n => Db.JsonHasKey(n.Payload, "name")));
        run.Select<CorpusNote>(G, "where-array-contains-string", q => q.Where(n => Db.JsonArrayContainsString(n.Payload, "tags", "red")));
        run.Select<CorpusNote>(G, "where-array-contains-any-several", q => q.Where(n => Db.JsonArrayContainsAny(n.Payload, "tags", values)));
        run.Select<CorpusNote>(G, "where-array-contains-any-one", q => q.Where(n => Db.JsonArrayContainsAny(n.Payload, "tags", single)));
        run.Select<CorpusNote>(G, "where-json-terms-parameter-order", q => q.Where(n => n.Rank > 1 && Db.JsonKeyEquals(n.Payload, "k", "v", JsonValueOrder.Text) && Db.JsonHasKey(n.Meta, "m")));
        run.Select<CorpusNote>(G, "where-moved-field-is-null", q => q.Where(n => Db.MovedFieldIsNull(n.Meta, legacy)));
        run.Select<CorpusNote>(G, "where-moved-field-equals-string", q => q.Where(n => Db.MovedFieldEquals(n.Meta, legacy, "x")));
        run.Select<CorpusNote>(G, "where-moved-field-equals-integer", q => q.Where(n => Db.MovedFieldEquals(n.Meta, legacy, 5)));
        run.Select<CorpusNote>(G, "where-moved-field-equals-guid", q => q.Where(n => Db.MovedFieldEquals(n.Meta, legacy, a)));
        run.Select<CorpusNote>(G, "where-moved-field-contains", q => q.Where(n => Db.MovedFieldContains(n.Meta, legacy, "a_b%")));
        run.Select<CorpusNote>(G, "where-moved-field-terms-combined", q => q.Where(n => Db.MovedFieldIsNull(n.Meta, other) || Db.MovedFieldEquals(n.Meta, legacy, "x")));
        run.Select<CorpusTask>(G, "where-json-on-soft-entity", q => q.Where(t => Db.JsonHasKey(t.Doc, "k")));

        run.Select<CorpusNote>(G, "order-json-key-ascending-nulls-last", q => q.OrderByJsonKey(n => n.Payload, new JsonKey("name"), false, false));
        run.Select<CorpusNote>(G, "order-json-key-descending-nulls-first", q => q.OrderByJsonKey(n => n.Payload, new JsonKey("name"), true, true));
        run.Select<CorpusNote>(G, "order-json-key-then-by", q => q.OrderBy(n => n.Title).ThenByJsonKey(n => n.Payload, new JsonKey("name"), true, false));
        run.Select<CorpusNote>(G, "order-json-key-parameter-order", q => q.Where(n => n.Title == "w").OrderByJsonKey(n => n.Payload, new JsonKey("k"), false, true));
        run.Select<CorpusNote>(G, "order-moved-field-number-or-epoch-millis", q => q.OrderByMovedFieldNumberOrEpochMillis(n => n.Meta, legacy, n => n.CreatedAt));

        JsonKeyOrdering Ordering(string key, bool desc, bool nullsFirst, JsonValueOrder order, params string[] arrangement) =>
            new(new JsonKey(key), desc, nullsFirst, order, arrangement);

        run.Select<CorpusNote>(G, "order-json-key-of-text", q => q.OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, Ordering("label", false, false, JsonValueOrder.Text)));
        run.Select<CorpusNote>(G, "order-json-key-of-number-descending", q => q.OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, Ordering("rank", true, true, JsonValueOrder.Number)));
        run.Select<CorpusNote>(G, "order-json-key-of-boolean", q => q.OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, Ordering("flag", false, true, JsonValueOrder.Boolean)));
        run.Select<CorpusNote>(G, "order-json-key-of-arranged", q => q.OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, Ordering("state", false, false, JsonValueOrder.Text, "open", "closed", "done")));
        run.Select<CorpusNote>(G, "order-json-key-of-soft-other-adds-live-term", q => q.OrderByJsonKeyOf<CorpusTask>((n, t) => t.ProjectId == n.OwnerId, t => t.Doc, Ordering("k", false, false, JsonValueOrder.Text)));
        run.Select<CorpusNote>(G, "order-json-key-of-then-by", q => q.OrderBy(n => n.Title).ThenByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, Ordering("rank", true, false, JsonValueOrder.Number)));
        run.Select<CorpusNote>(G, "order-json-key-of-with-correlation-value", q => q.OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id && t.Position > 1, t => t.Doc, Ordering("rank", false, false, JsonValueOrder.Number)));
        run.Select<CorpusNote>(G, "order-two-json-key-of-aliases", q => q.OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, Ordering("a", false, false, JsonValueOrder.Text)).ThenByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, Ordering("b", true, true, JsonValueOrder.Boolean)));

        run.Update<CorpusNote>(G, "set-json-merge", q => q.SetJsonMerge(n => n.Meta, Jsonb.Parse("{\"a\":1,\"b\":[true]}")).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "set-json-merge-after-other-sets", q => q.Set(n => n.Title, "t").SetJsonMerge(n => n.Meta, Jsonb.Parse("{\"a\":1}")).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "set-json-remove-then-merge", q => q.SetJsonRemoveThenMerge(n => n.Meta, [legacy, other], [third], Jsonb.Parse("{\"third_field\":1}")).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "set-json-remove-then-merge-only-clears", q => q.SetJsonRemoveThenMerge(n => n.Meta, [legacy], [], Jsonb.Parse("{}")).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "set-json-remove-then-merge-only-sets", q => q.SetJsonRemoveThenMerge(n => n.Meta, [], [third], Jsonb.Parse("{\"third_field\":2}")).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "set-json-member-increment", q => q.SetJsonMemberIncrement(n => n.Meta, legacy, 4).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "set-json-member-increment-negative", q => q.SetJsonMemberIncrement(n => n.Meta, legacy, -2).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "set-json-merge-beside-a-set-of-another-column", q => q.Set(n => n.Payload, Jsonb.Parse("{}")).SetJsonMerge(n => n.Meta, Jsonb.Parse("{\"a\":1}")).Where(n => n.Id == a));
        run.Update<CorpusTask>(G, "set-json-merge-soft-entity", q => q.SetJsonMerge(t => t.Doc, Jsonb.Parse("{\"x\":1}")).Where(t => t.Id == a));
        run.Rec.Statement(G, "update-returning-after-json-merge", () =>
        {
            var q = run.Q<CorpusNote>().SetJsonMerge(n => n.Meta, Jsonb.Parse("{\"a\":1}")).Where(n => n.Id == a);
            return CorpusRun.Read(q, q.CompileUpdateReturning([n => n.Meta]), q.RenderUpdateReturning([n => n.Meta]));
        });
    }
}
