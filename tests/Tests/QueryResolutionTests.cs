using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Neemle.OrmLite;
using Xunit;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests;

public sealed class QueryResolutionTests
{
    private const string OwnerTable = "resolution_owners";
    private const string Key = "resolution-distinct-key";

    private sealed record ResolutionOwner(Guid Id, string Name);

    private sealed record ResolutionUnregistered(Guid Id);

    static QueryResolutionTests()
    {
        OrmMeta.Register<ResolutionOwner>(OwnerTable, OwnerCol, ["id", "name"]);
        OrmMeta.Register<FixtureDocument>("fixture_documents", DocumentCol, ["id", "payload", "label"]);
    }

    private static string OwnerCol(string member) => member switch
    {
        nameof(ResolutionOwner.Id) => "id",
        nameof(ResolutionOwner.Name) => "name",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown ResolutionOwner member."),
    };

    private static string DocumentCol(string member) => member switch
    {
        nameof(FixtureDocument.Id) => "id",
        nameof(FixtureDocument.Payload) => "payload",
        nameof(FixtureDocument.Label) => "label",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureDocument member."),
    };

    private static OrmContext Pg() => TestContext.Of(new NeverUsedDataSource(), PostgresProvider.Instance);

    private static Func<DbDataReader, TRow> NoRows<TRow>() =>
        _ => throw new NotSupportedException("no rows are read here");

    private static Query<ResolutionOwner> Owners() => new(Pg(), NoRows<ResolutionOwner>());

    private static Query<FixtureDocument> Docs() => new(Pg(), NoRows<FixtureDocument>());

    private static Expression<Func<ResolutionOwner, FixtureDocument, bool>> Correlation() =>
        (o, d) => d.Id == o.Id;

    private static Expression<Func<FixtureDocument, object?>> Document() => d => d.Payload;

    private static JsonKeyOrdering Ordering(
        string key, bool descending, bool nullsFirst, JsonValueOrder order, string[] arrangement) =>
        new(new JsonKey(key), descending, nullsFirst, order, arrangement);

    private static Query<ResolutionOwner> FirstTerm(Query<ResolutionOwner> query) =>
        query.OrderByJsonKeyOf<FixtureDocument>(
            Correlation(), Document(), Ordering("first-key", false, false, JsonValueOrder.Text, []));

    [Fact(DisplayName = "the OrmMeta-resolved constructor renders the table and columns OrmMeta registered for a fixture")]
    public void The_resolved_constructor_renders_the_registered_table()
    {
        var filtered = Owners().Where(o => o.Name == "x");

        Assert.Contains("\"resolution_owners\"", filtered.RenderSelect(), StringComparison.Ordinal);
        Assert.Contains("\"resolution_owners\"", filtered.Set(o => o.Name, "y").RenderUpdate(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the OrmMeta-resolved constructor refuses a type nothing registered, with OrmMeta's own error")]
    public void An_unregistered_type_is_refused_with_the_OrmMeta_error()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => new Query<ResolutionUnregistered>(Pg(), NoRows<ResolutionUnregistered>()));

        Assert.Equal("No ORM metadata registered for " + typeof(ResolutionUnregistered).FullName, error.Message);
    }

    [Fact(DisplayName = "OrderByJsonKey and ThenByJsonKey with a JsonKey bind the key and never splice it into the statement")]
    public void The_single_table_overloads_bind_the_key()
    {
        var typedOrder = Docs().OrderByJsonKey(Document(), new JsonKey(Key), true, true);
        Assert.DoesNotContain(Key, typedOrder.RenderSelect(), StringComparison.Ordinal);
        Assert.Contains(Key, typedOrder.BoundParameters.OfType<string>());

        var typedThen = Docs().OrderByJsonKey(Document(), new JsonKey("first-key"), false, false)
            .ThenByJsonKey(Document(), new JsonKey(Key), false, true);
        var typedSql = typedThen.RenderSelect();

        Assert.DoesNotContain(Key, typedSql, StringComparison.Ordinal);
        Assert.Contains(Key, typedThen.BoundParameters.OfType<string>());
        Assert.Contains("@p1", typedSql, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "OrderByJsonKeyOf and ThenByJsonKeyOf with a JsonKeyOrdering bind the key and never splice it when the arrangement is empty")]
    [InlineData(JsonValueOrder.Text, false, false)]
    [InlineData(JsonValueOrder.Number, true, false)]
    [InlineData(JsonValueOrder.Boolean, false, true)]
    public void The_cross_table_overloads_bind_the_key_with_an_empty_arrangement(
        JsonValueOrder order, bool descending, bool nullsFirst) =>
        AssertCrossTableBinds(order, descending, nullsFirst, []);

    [Theory(DisplayName = "OrderByJsonKeyOf and ThenByJsonKeyOf with a JsonKeyOrdering bind the key and never splice it when the arrangement is not empty")]
    [InlineData(JsonValueOrder.Text, false, false)]
    [InlineData(JsonValueOrder.Number, true, true)]
    public void The_cross_table_overloads_bind_the_key_with_an_arrangement(
        JsonValueOrder order, bool descending, bool nullsFirst) =>
        AssertCrossTableBinds(order, descending, nullsFirst, ["urgent", "normal", "blocker"]);

    private static void AssertCrossTableBinds(
        JsonValueOrder order, bool descending, bool nullsFirst, string[] arrangement)
    {
        var ordering = Ordering(Key, descending, nullsFirst, order, arrangement);

        var typedOrder = Owners().OrderByJsonKeyOf<FixtureDocument>(Correlation(), Document(), ordering);
        Assert.DoesNotContain(Key, typedOrder.RenderSelect(), StringComparison.Ordinal);
        Assert.Contains(Key, typedOrder.BoundParameters.OfType<string>());

        var typedThen = FirstTerm(Owners()).ThenByJsonKeyOf<FixtureDocument>(Correlation(), Document(), ordering);
        Assert.DoesNotContain(Key, typedThen.RenderSelect(), StringComparison.Ordinal);
        Assert.Contains(Key, typedThen.BoundParameters.OfType<string>());
    }

    [Fact(DisplayName = "a null key is refused when the JsonKey is made and a default JsonKey is refused before any parameter is added")]
    public void A_null_key_and_a_default_key_are_refused()
    {
        var nullKey = Assert.Throws<ArgumentNullException>(() => new JsonKey(null!));
        Assert.Equal("key", nullKey.ParamName);

        Assert.Throws<InvalidOperationException>(() => default(JsonKey).Name);
        Assert.Equal(string.Empty, new JsonKey("").Name);

        var query = Docs();
        Assert.Throws<InvalidOperationException>(
            () => query.OrderByJsonKey(Document(), default(JsonKey), false, false));
        Assert.Empty(query.BoundParameters);
    }

    [Fact(DisplayName = "a null JsonKeyOrdering is refused by name and a null arrangement is refused when the ordering is made")]
    public void A_null_ordering_and_a_null_arrangement_are_refused()
    {
        var ordering = Assert.Throws<ArgumentNullException>(
            () => Owners().OrderByJsonKeyOf<FixtureDocument>(Correlation(), Document(), (JsonKeyOrdering)null!));
        Assert.Equal("ordering", ordering.ParamName);

        var thenOrdering = Assert.Throws<ArgumentNullException>(
            () => Owners().ThenByJsonKeyOf<FixtureDocument>(Correlation(), Document(), (JsonKeyOrdering)null!));
        Assert.Equal("ordering", thenOrdering.ParamName);

        var arrangement = Assert.Throws<ArgumentNullException>(
            () => new JsonKeyOrdering(new JsonKey("k"), false, false, JsonValueOrder.Text, null!));
        Assert.Equal("arrangement", arrangement.ParamName);
    }

    [Fact(DisplayName = "an undefined OnConflict value is refused by RenderUpsert and by UpsertAsync before any SQL is rendered, and the defined values still render")]
    public async Task An_undefined_conflict_action_is_refused_before_anything_is_rendered()
    {
        var row = new Row<ResolutionOwner>().With(o => o.Id, Guid.NewGuid()).With(o => o.Name, "n");
        Expression<Func<ResolutionOwner, object?>>[] conflict = [o => o.Id];
        var undefined = (OnConflict)99;

        var rendered = Assert.Throws<ArgumentOutOfRangeException>(
            () => Owners().RenderUpsert(row, conflict, undefined));
        Assert.Equal("action", rendered.ParamName);
        Assert.Contains("OnConflict has no value 99", rendered.Message, StringComparison.Ordinal);

        var executed = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Owners().UpsertAsync(row, conflict, undefined));
        Assert.Equal("action", executed.ParamName);

        Assert.Contains(
            "ON CONFLICT", Owners().RenderUpsert(row, conflict, OnConflict.DoNothing).Sql, StringComparison.Ordinal);
        Assert.Contains(
            "DO UPDATE",
            Owners().RenderUpsert(
                row, conflict, OnConflict.DoUpdate, [UpsertSet<ResolutionOwner>.Inserted(o => o.Name)]).Sql,
            StringComparison.Ordinal);
    }
}
