using System.Data.Common;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public abstract class JsonBehaviourSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    private OrmContext Ctx => Fixture.Context;

    private static readonly MovedFieldKey K = new("k");

    private static readonly MovedFieldKey NumberKey = new("n");

    private Query<BehaveNote> Notes() => new(Ctx, BehaveNote.Map);

    private static Row<BehaveNote> DocRow(Guid tenant, string title, string? json, DateTime? stamped = null) =>
        DocRowOf(tenant, title, json is null ? null : Jsonb.Parse(json), stamped);

    private static Row<BehaveNote> DocRowOf(Guid tenant, string title, Jsonb? document, DateTime? stamped = null)
    {
        var row = new Row<BehaveNote>()
            .With(n => n.Id, Guid.NewGuid())
            .With(n => n.TenantId, tenant)
            .With(n => n.Title, title)
            .With(n => n.Stars, 1)
            .With(n => n.Doc, document);
        return stamped is null ? row : row.With(n => n.StampedAt, stamped);
    }

    private async Task<Guid> SeedAsync(params (string Title, string? Doc)[] rows)
    {
        var tenant = Guid.NewGuid();
        await Notes().InsertManyAsync(rows.Select(r => DocRow(tenant, r.Title, r.Doc)).ToList());
        return tenant;
    }

    private static string J(string text) => text.Replace("^u", "\\u", StringComparison.Ordinal);

    private static List<string> Titles(IEnumerable<BehaveNote> notes) => notes.Select(n => n.Title).ToList();

    private async Task<List<string>> TitlesWhereAsync(Guid tenant, System.Linq.Expressions.Expression<Func<BehaveNote, bool>> predicate) =>
        Titles(await Notes().Where(n => n.TenantId == tenant).Where(predicate).OrderBy(n => n.Title).ToListAsync());

    private static JsonKeyOrdering Ordering(string key, JsonValueOrder order, bool descending = false, bool nullsFirst = false, params string[] arrangement) =>
        new(new JsonKey(key), descending, nullsFirst, order, arrangement);

    private Query<BehaveNote> OrderedBy(Guid tenant, JsonKeyOrdering ordering) =>
        Notes().Where(n => n.TenantId == tenant)
            .OrderByJsonKeyOf<BehaveNote>((a, b) => b.Id == a.Id, b => b.Doc, ordering)
            .ThenBy(n => n.Title);

    [SkippableFact(DisplayName = "a document is stored in the canonical jsonb form: keys ordered by length then bytes, the last duplicate wins, numbers and escapes as PostgreSQL prints them, non-ASCII text unescaped")]
    public async Task Documents_are_stored_canonically()
    {
        Fixture.RequireAvailable();
        var source = J("""{"b":1,"a":[1,2,{"x":null}],"aa":"é^u00e9","c":true,"d":1.50,"e":1e2,"f":-0.0,"g":1E-3,"h":12345678901234567890123,"dup":1,"dup":2,"ctl":"^u0001\n\"\\\/"}""");
        const string canonical = """{"a": [1, 2, {"x": null}], "b": 1, "c": true, "d": 1.50, "e": 100, "f": 0.0, "g": 0.001, "h": 12345678901234567890123, "aa": "éé", "ctl": "\u0001\n\"\\/", "dup": 2}""";
        var tenant = await SeedAsync(("c1", source));

        var read = Assert.Single(await Notes().Where(n => n.TenantId == tenant).ToListAsync());

        Assert.Equal(canonical, read.Doc!.Json);
        Assert.True(Jsonb.SameDocument(Jsonb.Parse(source), read.Doc));
    }

    [SkippableFact(DisplayName = "a key read compares the extracted text exactly: case matters, JSON null and a missing member read as NULL, and a nested value reads as its canonical text")]
    public async Task Key_reads_compare_text()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("r1", """{"kind":"red","n":9}"""),
            ("r2", """{"kind":"blue","n":10}"""),
            ("r3", """{"kind":null,"n":"11"}"""),
            ("r4", """{"other":1,"n":1.5}"""),
            ("r5", null),
            ("r6", """{"kind":"Red"}"""),
            ("r7", """{"kind":{"b":"deep"}}"""));

        Assert.Equal(["r1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "kind", "red", JsonValueOrder.Text)));
        Assert.Equal(["r6"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "kind", "Red", JsonValueOrder.Text)));
        Assert.Equal(["r2"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "n", "10", JsonValueOrder.Text)));
        Assert.Equal(["r3"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "n", "11", JsonValueOrder.Text)));
        Assert.Equal(["r4"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "n", "1.5", JsonValueOrder.Text)));
        Assert.Equal(["r7"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "kind", "{\"b\": \"deep\"}", JsonValueOrder.Text)));
    }

    [SkippableFact(DisplayName = "a key is a member name and never a path: dots, dollars, brackets, quotes, spaces, the empty name and non-ASCII names match only the member of that exact name")]
    public async Task Keys_are_member_names()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("k1", """{"a":{"b":"deep"},"a.b":"flat","$x":"dollar","[0]":"idx","":"empty","k\"q":"quote","sp ace":"s","ключ":"Іван","$.a":"path"}"""),
            ("k2", """{"a.b":"other","a":{"b":"flat"}}"""));

        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "a.b", "flat", JsonValueOrder.Text)));
        Assert.Equal(["k2"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "a.b", "other", JsonValueOrder.Text)));
        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "$x", "dollar", JsonValueOrder.Text)));
        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "$.a", "path", JsonValueOrder.Text)));
        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "[0]", "idx", JsonValueOrder.Text)));
        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "", "empty", JsonValueOrder.Text)));
        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "k\"q", "quote", JsonValueOrder.Text)));
        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "sp ace", "s", JsonValueOrder.Text)));
        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "ключ", "Іван", JsonValueOrder.Text)));
        Assert.Equal(["k1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "a", "{\"b\": \"deep\"}", JsonValueOrder.Text)));
        Assert.Empty(await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "b", "deep", JsonValueOrder.Text)));
    }

    [SkippableFact(DisplayName = "a key exists when an object has the member, even as JSON null, when an array holds that string or when the whole document is that string, and negating it drops SQL NULL")]
    public async Task Key_existence_follows_jsonb_exists()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("h1", """{"kind":null}"""),
            ("h2", """{"other":1}"""),
            ("h3", """["kind","x"]"""),
            ("h4", "\"kind\""),
            ("h5", "5"),
            ("h6", "null"),
            ("h7", """{"nested":{"kind":1}}"""),
            ("h8", null),
            ("h9", """[1,"y"]"""),
            ("ha", """["kind2"]"""));

        Assert.Equal(["h1", "h3", "h4"], await TitlesWhereAsync(tenant, n => Db.JsonHasKey(n.Doc, "kind")));
        Assert.Equal(["h2", "h5", "h6", "h7", "h9", "ha"], await TitlesWhereAsync(tenant, n => !Db.JsonHasKey(n.Doc, "kind")));
        Assert.Equal(["h9"], await TitlesWhereAsync(tenant, n => Db.JsonHasKey(n.Doc, "y")));
        Assert.Empty(await TitlesWhereAsync(tenant, n => Db.JsonHasKey(n.Doc, "1")));
        Assert.Empty(await TitlesWhereAsync(tenant, n => Db.JsonHasKey(n.Doc, "5")));
    }

    [SkippableFact(DisplayName = "ordering by a key as text sorts 10 before 9 and puts missing, null and SQL NULL where the nulls position says, on both providers")]
    public async Task Text_ordering_is_text()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("t1", """{"n":9}"""), ("t2", """{"n":10}"""), ("t3", """{"n":"11"}"""), ("t4", """{"n":1}"""), ("t5", """{"n":true}"""),
            ("t6", """{"n":"abc"}"""), ("t7", """{"n":""}"""), ("t8", """{"x":1}"""), ("t9", """{"n":null}"""), ("ta", null));

        var ascending = await Notes().Where(n => n.TenantId == tenant).OrderByJsonKey(n => n.Doc, new JsonKey("n"), false, false).ThenBy(n => n.Title).ToListAsync();
        var descending = await Notes().Where(n => n.TenantId == tenant).OrderByJsonKey(n => n.Doc, new JsonKey("n"), true, true).ThenBy(n => n.Title).ToListAsync();

        Assert.Equal(["t7", "t4", "t2", "t3", "t1", "t6", "t5", "t8", "t9", "ta"], Titles(ascending));
        Assert.Equal(["t8", "t9", "ta", "t5", "t6", "t1", "t3", "t2", "t4", "t7"], Titles(descending));
    }

    [SkippableFact(DisplayName = "ordering by a key as a number sorts 9 before 10 across numbers and numeric text, and every non-number ranks NULL")]
    public async Task Number_ordering_is_numeric()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("t1", """{"n":9}"""), ("t2", """{"n":10}"""), ("t3", """{"n":"11"}"""), ("t4", """{"n":1.5}"""), ("t5", """{"n":true}"""),
            ("t6", """{"n":"abc"}"""), ("t7", """{"n":""}"""), ("t8", """{"x":1}"""), ("t9", """{"n":null}"""), ("ta", null), ("tb", """{"n":-2e0}"""), ("tc", """{"n":" 3 "}"""));

        var ascending = await OrderedBy(tenant, Ordering("n", JsonValueOrder.Number)).ToListAsync();
        var descending = await OrderedBy(tenant, Ordering("n", JsonValueOrder.Number, descending: true, nullsFirst: true)).ToListAsync();

        Assert.Equal(["tb", "t4", "tc", "t1", "t2", "t3", "t5", "t6", "t7", "t8", "t9", "ta"], Titles(ascending));
        Assert.Equal(["t5", "t6", "t7", "t8", "t9", "ta", "t3", "t2", "t1", "tc", "t4", "tb"], Titles(descending));
    }

    [SkippableFact(DisplayName = "ordering by a key as a boolean accepts true, false, yes, off, t and 1 the way PostgreSQL does, and ranks every other text NULL")]
    public async Task Boolean_ordering_is_boolean()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("b1", """{"f":false}"""), ("b2", """{"f":true}"""), ("b3", """{"f":"yes"}"""), ("b4", """{"f":"off"}"""), ("b5", """{"f":"abc"}"""),
            ("b6", """{"f":"T"}"""), ("b7", """{"f":1}"""), ("b8", """{"g":1}"""), ("b9", """{"f":0}"""), ("ba", """{"f":"o"}"""), ("bb", """{"f":" Tr "}"""));

        var ordered = await OrderedBy(tenant, Ordering("f", JsonValueOrder.Boolean)).ToListAsync();

        Assert.Equal(["b1", "b4", "b9", "b2", "b3", "b6", "b7", "bb", "b5", "b8", "ba"], Titles(ordered));
    }

    [SkippableFact(DisplayName = "an equality on a key states how it compares: text matches the text, a number matches 10, 10.0, \"1e1\" and hex text alike, a boolean matches yes and 1")]
    public async Task Typed_equality_follows_the_stated_order()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("e1", """{"k":10}"""), ("e2", """{"k":10.0}"""), ("e3", """{"k":"10"}"""), ("e4", """{"k":"1e1"}"""), ("e5", """{"k":11}"""),
            ("e6", """{"k":"abc"}"""), ("e7", """{"k":true}"""), ("e8", """{"k":" 10 "}"""), ("e9", """{"k":"0x0A"}"""), ("ea", """{"k":null}"""),
            ("f1", """{"k":"yes"}"""), ("f2", """{"k":"tr"}"""), ("f3", """{"k":"on"}"""), ("f4", """{"k":1}"""), ("f5", """{"k":false}"""), ("f6", """{"k":"o"}"""),
            ("f7", """{"k":"maybe"}"""), ("f8", """{"k":"n"}"""));

        Assert.Equal(["e1", "e3"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "k", "10", JsonValueOrder.Text)));
        Assert.Equal(["e1", "e2", "e3", "e4", "e8", "e9"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "k", "10", JsonValueOrder.Number)));
        Assert.Equal(["e1", "e2", "e3", "e4", "e8", "e9"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "k", "1e1", JsonValueOrder.Number)));
        Assert.Equal(["e5"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "k", "11.0", JsonValueOrder.Number)));
        Assert.Equal(["e7", "f1", "f2", "f3", "f4"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "k", "t", JsonValueOrder.Boolean)));
        Assert.Equal(["f5", "f8"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "k", "no", JsonValueOrder.Boolean)));
    }

    [SkippableFact(DisplayName = "a moved field reads the member as text: JSON null, a missing member and SQL NULL are all NULL, booleans and decimals compare as PostgreSQL prints them, contains folds case in any script and escapes the wildcards")]
    public async Task Moved_fields_resolve_as_text()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("m1", """{"k":null}"""), ("m2", """{"x":1}"""), ("m3", """{"k":"v"}"""), ("m4", null), ("m5", """{"k":7}"""), ("m6", """{"k":true}"""),
            ("m7", """{"k":12.50}"""), ("m8", """{"k":"Іван"}"""), ("m9", """{"k":"a_b"}"""), ("ma", """{"k":"axb"}"""), ("mb", """{"k":"100%"}"""));

        Assert.Equal(["m1", "m2", "m4"], await TitlesWhereAsync(tenant, n => Db.MovedFieldIsNull(n.Doc, K)));
        Assert.Equal(["m3"], await TitlesWhereAsync(tenant, n => Db.MovedFieldEquals(n.Doc, K, "v")));
        Assert.Equal(["m5"], await TitlesWhereAsync(tenant, n => Db.MovedFieldEquals(n.Doc, K, 7)));
        Assert.Equal(["m6"], await TitlesWhereAsync(tenant, n => Db.MovedFieldEquals(n.Doc, K, true)));
        Assert.Equal(["m7"], await TitlesWhereAsync(tenant, n => Db.MovedFieldEquals(n.Doc, K, 12.50m)));
        Assert.Empty(await TitlesWhereAsync(tenant, n => Db.MovedFieldEquals(n.Doc, K, 12.5m)));
        Assert.Equal(["m8"], await TitlesWhereAsync(tenant, n => Db.MovedFieldContains(n.Doc, K, "ВАН")));
        Assert.Equal(["m8"], await TitlesWhereAsync(tenant, n => Db.MovedFieldContains(n.Doc, K, "іван")));
        Assert.Equal(["m9"], await TitlesWhereAsync(tenant, n => Db.MovedFieldContains(n.Doc, K, "A_B")));
        Assert.Equal(["mb"], await TitlesWhereAsync(tenant, n => Db.MovedFieldContains(n.Doc, K, "0%")));
    }

    [SkippableFact(DisplayName = "array membership matches string elements only, a missing member is NULL and a wrong-typed one is false, so a negation keeps the second and drops the first")]
    public async Task Array_membership_is_three_valued()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("a1", """{"t":["x","y"]}"""), ("a2", """{"t":["z",1]}"""), ("a3", """{"t":[1,2]}"""), ("a4", """{"t":"x"}"""), ("a5", """{"u":["x"]}"""),
            ("a6", """{"t":null}"""), ("a7", null), ("a8", """["x"]"""), ("a9", """{"t":[["x"]]}"""), ("aa", """{"t":{"x":1}}"""));
        IReadOnlyList<string> anyOf = ["x", "z"];

        Assert.Equal(["a1"], await TitlesWhereAsync(tenant, n => Db.JsonArrayContainsString(n.Doc, "t", "x")));
        Assert.Equal(["a2", "a3", "a4", "a6", "a9", "aa"], await TitlesWhereAsync(tenant, n => !Db.JsonArrayContainsString(n.Doc, "t", "x")));
        Assert.Equal(["a1", "a2"], await TitlesWhereAsync(tenant, n => Db.JsonArrayContainsAny(n.Doc, "t", anyOf)));
        Assert.Equal(["a3", "a4", "a6", "a9", "aa"], await TitlesWhereAsync(tenant, n => !Db.JsonArrayContainsAny(n.Doc, "t", anyOf)));
        Assert.Equal(["a2"], await TitlesWhereAsync(tenant, n => Db.JsonArrayContainsString(n.Doc, "t", "z")));
        Assert.Empty(await TitlesWhereAsync(tenant, n => Db.JsonArrayContainsString(n.Doc, "t", "1")));
    }

    [SkippableFact(DisplayName = "an arranged order ranks each row by the position of its value in the arrangement and puts unlisted and missing values after the listed ones")]
    public async Task Arranged_order_ranks_by_position()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("r1", """{"p":"mid"}"""), ("r2", """{"p":"high"}"""), ("r3", """{"p":"low"}"""), ("r4", """{"p":"zzz"}"""), ("r5", """{"x":1}"""),
            ("r6", """{"p":"low"}"""), ("r7", """{"p":null}"""), ("r8", null));
        string[] arrangement = ["low", "mid", "high"];

        var ascending = await OrderedBy(tenant, Ordering("p", JsonValueOrder.Text, false, false, arrangement)).ToListAsync();
        var descending = await OrderedBy(tenant, Ordering("p", JsonValueOrder.Text, true, true, arrangement)).ToListAsync();

        Assert.Equal(["r3", "r6", "r1", "r2", "r4", "r5", "r7", "r8"], Titles(ascending));
        Assert.Equal(["r4", "r5", "r7", "r8", "r2", "r1", "r3", "r6"], Titles(descending));
    }

    [SkippableFact(DisplayName = "the moved-number order uses the member when it reads as a double and the row's own timestamp in whole milliseconds, floored, when it does not")]
    public async Task Moved_number_falls_back_to_epoch_milliseconds()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await Notes().InsertManyAsync([
            DocRow(tenant, "p1", """{"ts":"3000"}""", epoch.AddSeconds(1)),
            DocRow(tenant, "p2", """{"ts":2000}""", epoch.AddSeconds(5)),
            DocRow(tenant, "p3", """{"ts":"abc"}""", epoch.AddSeconds(4)),
            DocRow(tenant, "p4", null, epoch.AddMilliseconds(500)),
            DocRow(tenant, "p5", """{"ts":null}""", epoch.AddSeconds(6)),
            DocRow(tenant, "p6", """{"ts":1e3}""", epoch.AddSeconds(9)),
            DocRow(tenant, "p7", """{"x":1}""", epoch.AddSeconds(2).AddTicks(16000)),
            DocRow(tenant, "pc", """{"x":1}""", epoch.AddSeconds(2).AddTicks(19999)),
            DocRow(tenant, "p8", """{"ts":"1_0"}""", epoch.AddSeconds(8).AddTicks(5000)),
            DocRow(tenant, "pa", """{"ts":2001.5}""", epoch.AddSeconds(11)),
            DocRow(tenant, "p9", """{"ts":" 2.5e3 "}""", epoch.AddSeconds(10)),
        ]);

        var ordered = await Notes().Where(n => n.TenantId == tenant)
            .OrderByMovedFieldNumberOrEpochMillis(n => n.Doc, new MovedFieldKey("ts"), n => n.StampedAt)
            .ThenBy(n => n.Title).ToListAsync();

        Assert.Equal(["p4", "p6", "p2", "p7", "pa", "pc", "p9", "p1", "p3", "p5", "p8"], Titles(ordered));
    }

    [SkippableFact(DisplayName = "a merge puts the patch over the document member by member and replaces a nested object whole, keeps patch nulls, and starts from an empty object when the document is NULL")]
    public async Task Merge_is_shallow_and_patch_wins()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(("w1", """{"a":1,"b":{"x":1},"keep":true}"""), ("w2", null), ("w3", """{"a":1}"""));

        var merged = await Notes().Where(n => n.TenantId == tenant && n.Title == "w1")
            .SetJsonMerge(n => n.Doc, Jsonb.Parse("""{"b":{"y":2},"c":null,"a":9}""")).UpdateAsync();
        var fromNull = await Notes().Where(n => n.TenantId == tenant && n.Title == "w2")
            .SetJsonMerge(n => n.Doc, Jsonb.Parse("""{"z":1}""")).UpdateAsync();
        var emptyPatch = await Notes().Where(n => n.TenantId == tenant && n.Title == "w3")
            .SetJsonMerge(n => n.Doc, Jsonb.Parse("{}")).UpdateAsync();

        var rows = (await Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Title).ToListAsync()).ToDictionary(n => n.Title, n => n.Doc!.Json);
        Assert.Equal((1, 1, 1), (merged, fromNull, emptyPatch));
        Assert.Equal("""{"a": 9, "b": {"y": 2}, "c": null, "keep": true}""", rows["w1"]);
        Assert.Equal("""{"z": 1}""", rows["w2"]);
        Assert.Equal("""{"a": 1}""", rows["w3"]);
    }

    [SkippableFact(DisplayName = "remove-then-merge drops the cleared members, ignores one that is absent, then lays the patch over what is left")]
    public async Task Remove_then_merge()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(("w1", """{"a":1,"drop":1,"keep":2}"""), ("w2", null), ("w3", """{"drop":null}"""));
        IReadOnlyList<MovedFieldKey> cleared = [new("drop"), new("gone")];
        IReadOnlyList<MovedFieldKey> set = [new("keep")];

        var changed = await Notes().Where(n => n.TenantId == tenant)
            .SetJsonRemoveThenMerge(n => n.Doc, cleared, set, Jsonb.Parse("""{"keep":3,"new":true}""")).UpdateAsync();

        var rows = (await Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Title).ToListAsync()).ToDictionary(n => n.Title, n => n.Doc!.Json);
        Assert.Equal(3, changed);
        Assert.Equal("""{"a": 1, "new": true, "keep": 3}""", rows["w1"]);
        Assert.Equal("""{"new": true, "keep": 3}""", rows["w2"]);
        Assert.Equal("""{"new": true, "keep": 3}""", rows["w3"]);
    }

    [SkippableFact(DisplayName = "a member increment reads the member as text, counts a missing or empty one as zero, keeps the other members and refuses a member that is not an integer, changing nothing")]
    public async Task Member_increment()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("w4", """{"n":5,"o":1}"""), ("w5", """{"n":""}"""), ("w7", """{"x":1}"""), ("w8", null), ("w9", """{"n":" -4 "}"""), ("wa", """{"n":"1_000"}"""), ("wb", """{"n":null}"""));
        var key = new MovedFieldKey("n");

        var changed = await Notes().Where(n => n.TenantId == tenant && n.Title != "w6").SetJsonMemberIncrement(n => n.Doc, key, 3).UpdateAsync();

        var rows = (await Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Title).ToListAsync()).ToDictionary(n => n.Title, n => n.Doc!.Json);
        Assert.Equal(7, changed);
        Assert.Equal("""{"n": 8, "o": 1}""", rows["w4"]);
        Assert.Equal("""{"n": 3}""", rows["w5"]);
        Assert.Equal("""{"n": 3, "x": 1}""", rows["w7"]);
        Assert.Equal("""{"n": 3}""", rows["w8"]);
        Assert.Equal("""{"n": -1}""", rows["w9"]);
        Assert.Equal("""{"n": 1003}""", rows["wa"]);
        Assert.Equal("""{"n": 3}""", rows["wb"]);
    }

    [SkippableFact(DisplayName = "a member that is not an integer, a fraction, a word or a value past int32, makes the increment fail and leaves the document as it was")]
    public async Task Increment_refuses_non_integers()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(("x1", """{"n":"abc"}"""), ("x2", """{"n":1.5}"""), ("x3", """{"n":99999999999}"""), ("x4", """{"n":true}"""));
        var key = new MovedFieldKey("n");

        foreach (var title in new[] { "x1", "x2", "x3", "x4" })
        {
            await Assert.ThrowsAnyAsync<DbException>(() => Notes().Where(n => n.TenantId == tenant && n.Title == title)
                .SetJsonMemberIncrement(n => n.Doc, key, 1).UpdateAsync());
        }

        var rows = (await Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Title).ToListAsync()).Select(n => n.Doc!.Json).ToList();
        Assert.Equal(["""{"n": "abc"}""", """{"n": 1.5}""", """{"n": 99999999999}""", """{"n": true}"""], rows);
    }

    [SkippableFact(DisplayName = "integers past int64 keep every digit as text and read back unchanged")]
    public async Task Large_integers_keep_their_digits()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("g1", """{"n":12345678901234567890,"m":9007199254740993,"d":0.1000000000000000055511151231257827}"""),
            ("g2", """{"m":9007199254740992}"""));

        var text = await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "n", "12345678901234567890", JsonValueOrder.Text));
        var exact = await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "m", "9007199254740993", JsonValueOrder.Text));
        var moved = await TitlesWhereAsync(tenant, n => Db.MovedFieldEquals(n.Doc, NumberKey, "12345678901234567890"));
        var read = Assert.Single(await Notes().Where(n => n.TenantId == tenant && n.Title == "g1").ToListAsync());

        Assert.Equal(["g1"], text);
        Assert.Equal(["g1"], exact);
        Assert.Equal(["g1"], moved);
        Assert.Equal("""{"d": 0.1000000000000000055511151231257827, "m": 9007199254740993, "n": 12345678901234567890}""", read.Doc!.Json);
    }

    [SkippableFact(DisplayName = "a numeric comparison is exact on PostgreSQL and IEEE double on SQLite, so two integers that differ past 2^53 compare equal only on SQLite")]
    public async Task Numeric_comparison_precision_is_the_documented_difference()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(("g1", """{"m":9007199254740993}"""), ("g2", """{"m":9007199254740992}"""));

        var numeric = await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "m", "9007199254740992", JsonValueOrder.Number));

        Assert.Equal(Fixture.ProviderId == "sqlite" ? ["g1", "g2"] : ["g2"], numeric);
    }

    [SkippableFact(DisplayName = "unicode survives the round trip: an escaped and a raw spelling are the same text, keys and values match, a surrogate pair is one character, and the stored text is not escaped")]
    public async Task Unicode_round_trips()
    {
        Fixture.RequireAvailable();
        var tenant = await SeedAsync(
            ("u1", J("""{"name":"^u0406^u0432^u0430^u043d","^u00e9":"^u00fc"}""")),
            ("u2", """{"name":"Іван"}"""),
            ("u3", """{"name":"😀"}"""),
            ("u4", J("""{"name":"^ud83d^ude00"}""")),
            ("u5", """{"name":"Іван ","ключ":1}"""));

        Assert.Equal(["u1", "u2"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "name", "Іван", JsonValueOrder.Text)));
        Assert.Equal(["u3", "u4"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "name", "😀", JsonValueOrder.Text)));
        Assert.Equal(["u1"], await TitlesWhereAsync(tenant, n => Db.JsonKeyEquals(n.Doc, "é", "ü", JsonValueOrder.Text)));
        Assert.Equal(["u5"], await TitlesWhereAsync(tenant, n => Db.JsonHasKey(n.Doc, "ключ")));
        var u1 = Assert.Single(await Notes().Where(n => n.TenantId == tenant && n.Title == "u1").ToListAsync());
        var u4 = Assert.Single(await Notes().Where(n => n.TenantId == tenant && n.Title == "u4").ToListAsync());
        Assert.Equal("""{"é": "ü", "name": "Іван"}""", u1.Doc!.Json);
        Assert.Equal("""{"name": "😀"}""", u4.Doc!.Json);
    }

    [SkippableTheory(DisplayName = "text that is not a JSON document is refused on the way in, by the library and by the column alone")]
    [InlineData("{bad")]
    [InlineData("{a:1}")]
    [InlineData("[1,]")]
    [InlineData("")]
    [InlineData("01")]
    [InlineData("NaN")]
    [InlineData("'x'")]
    [InlineData("{\"a\":1} x")]
    public async Task Invalid_documents_are_refused(string text)
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<DbException>(() => Notes().InsertManyAsync([
            DocRowOf(tenant, "bad", Jsonb.FromStorage(text)),
        ]));
        await Assert.ThrowsAnyAsync<DbException>(() => Fixture.ExecuteAsync(
            $"INSERT INTO behave_notes (id, tenant_id, title, stars, doc) VALUES ('{Guid.NewGuid()}', '{tenant}', 'raw', 1, '{text.Replace("'", "''", StringComparison.Ordinal)}')"));
        Assert.Equal(0, await Notes().Where(n => n.TenantId == tenant).CountAsync());
    }

    [SkippableTheory(DisplayName = "text PostgreSQL refuses but a column check alone would pass, a NUL escape and a lone surrogate, is refused by the library on both providers")]
    [InlineData("""{"a":"\u0000"}""")]
    [InlineData("""{"a":"\ud800"}""")]
    public async Task The_library_refuses_what_jsonb_refuses(string text)
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<DbException>(() => Notes().InsertManyAsync([
            DocRowOf(tenant, "bad", Jsonb.FromStorage(text)),
        ]));
        Assert.Equal(0, await Notes().Where(n => n.TenantId == tenant).CountAsync());
    }
}

public sealed class SqliteJsonBehaviourTests(SqliteFixture fixture) : JsonBehaviourSuite<SqliteFixture>(fixture);

public sealed class PostgresJsonBehaviourTests(PostgresFixture fixture) : JsonBehaviourSuite<PostgresFixture>(fixture);
