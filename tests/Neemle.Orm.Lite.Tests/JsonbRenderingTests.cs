using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class JsonbRenderingTests
{
    static JsonbRenderingTests() =>
        OrmMeta.Register<FixtureDocument>("fixture_documents", Col, ["id", "payload", "label"]);

    private static OrmContext Pg() => TestContext.Of(new NeverUsedDataSource(), new PgDialect());

    private static OrmContext Sqlite() => TestContext.Of(new NeverUsedDataSource(), new SqliteDialect());

    private static Query<FixtureDocument> On(OrmContext ctx) =>
        new(ctx, _ => throw new NotSupportedException("no rows are read here"));

    private static Query<FixtureDocument> Query() => On(Pg());

    private static string Col(string member) => member switch
    {
        nameof(FixtureDocument.Id) => "id",
        nameof(FixtureDocument.Payload) => "payload",
        nameof(FixtureDocument.Label) => "label",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureDocument member."),
    };

    private static Jsonb Document() => Jsonb.Parse("{\"tier\":\"gold\"}");

    private static Row<FixtureDocument> RowCarryingADocument() => new Row<FixtureDocument>()
        .With(d => d.Id, Guid.NewGuid())
        .With(d => d.Payload, Document())
        .With(d => d.Label, "a plain string neighbour");

    [Fact]
    public void A_document_in_the_VALUES_list_carries_the_cast_and_the_string_beside_it_does_not()
    {
        var (sql, parameters) = Query().RenderUpsert(
            RowCarryingADocument(), [d => d.Id], OnConflict.DoNothing);

        Assert.Contains("VALUES (@p0, @p1::jsonb, @p2)", sql);
        Assert.Equal(3, parameters.Count);
        Assert.IsType<Jsonb>(parameters[1]);
    }

    [Fact]
    public void The_DO_UPDATE_assignment_carries_the_cast_for_a_document_and_not_for_its_neighbour()
    {
        var (sql, _) = Query().RenderUpsert(
            RowCarryingADocument(),
            [d => d.Id],
            OnConflict.DoUpdate,
            sets: [UpsertSet<FixtureDocument>.Inserted(d => d.Payload), UpsertSet<FixtureDocument>.Inserted(d => d.Label)]);

        Assert.Contains("DO UPDATE SET \"payload\" = @p3::jsonb, \"label\" = @p4", sql);
    }

    [Fact]
    public void InsertMany_casts_the_document_row_and_leaves_the_null_document_row_uncast()
    {
        var (sql, parameters) = Query().RenderInsertMany(new[]
        {
            RowCarryingADocument(),
            new Row<FixtureDocument>()
                .With(d => d.Id, Guid.NewGuid())
                .With(d => d.Payload, null)
                .With(d => d.Label, "the row whose document is absent"),
        });

        Assert.Contains("VALUES (@p0, @p1::jsonb, @p2), (@p3, @p4, @p5)", sql);
        Assert.Equal(6, parameters.Count);
    }

    [Fact]
    public void Set_casts_a_document_and_leaves_every_other_value_alone()
    {
        var sql = Query()
            .Set(d => d.Payload, Document())
            .Set(d => d.Label, "still an ordinary string")
            .Where(d => d.Label == "any")
            .RenderUpdate();

        Assert.Contains("SET \"payload\" = @p0::jsonb, \"label\" = @p1", sql);
    }

    [Fact]
    public void SetCoalesce_puts_the_cast_inside_the_COALESCE_where_the_value_is()
    {
        var sql = Query()
            .SetCoalesce(d => d.Payload, Document())
            .Where(d => d.Label == "any")
            .RenderUpdate();

        Assert.Contains("\"payload\" = COALESCE(@p0::jsonb, \"payload\")", sql);
    }

    [Fact]
    public void A_WHERE_comparing_the_column_to_a_document_carries_the_cast()
    {
        var document = Document();

        var sql = Query().Where(d => d.Payload == document).RenderSelect();

        Assert.Contains("WHERE \"payload\" = @p0::jsonb", sql);
    }

    [Fact]
    public void Comparing_the_column_to_null_stays_IS_NULL_and_gains_no_cast()
    {
        var sql = Query().Where(d => d.Payload == null).RenderSelect();

        Assert.Contains("WHERE \"payload\" IS NULL", sql);
        Assert.DoesNotContain("::jsonb", sql);
    }

    [Fact]
    public void The_key_predicate_binds_the_key_AND_the_value_and_renders_neither_into_the_text()
    {
        var query = Query().Where(d => Db.JsonKeyEquals(d.Payload, "tier", "gold", JsonValueOrder.Text));

        var sql = query.RenderSelect();

        Assert.Contains("WHERE (\"payload\" ->> @p0) = @p1", sql);
        Assert.DoesNotContain("tier", sql);
        Assert.DoesNotContain("gold", sql);
    }

    [Fact]
    public void The_key_existence_predicate_renders_the_function_form_over_a_bound_key()
    {
        var sql = Query().Where(d => Db.JsonHasKey(d.Payload, "tier")).RenderSelect();

        Assert.Contains("WHERE jsonb_exists(\"payload\", @p0)", sql);
        Assert.DoesNotContain("tier", sql);
    }

    [Fact]
    public void The_two_key_predicates_are_DIFFERENT_operators_and_not_two_spellings_of_one()
    {
        var text = Query().Where(d => Db.JsonKeyEquals(d.Payload, "tier", "gold", JsonValueOrder.Text)).RenderSelect();
        var exists = Query().Where(d => Db.JsonHasKey(d.Payload, "tier")).RenderSelect();

        Assert.NotEqual(text, exists);
        Assert.Contains("->>", text);
        Assert.DoesNotContain("->>", exists);
        Assert.Contains("jsonb_exists", exists);
        Assert.DoesNotContain("jsonb_exists", text);
    }

    [Fact]
    public void The_ordering_states_the_position_of_key_absent_rows_in_both_directions()
    {
        Assert.Contains(
            "ORDER BY (\"payload\" ->> @p0) ASC NULLS LAST",
            Query().OrderByJsonKey(d => d.Payload, new JsonKey("tier"), descending: false, nullsFirst: false).RenderSelect());

        Assert.Contains(
            "ORDER BY (\"payload\" ->> @p0) DESC NULLS FIRST",
            Query().OrderByJsonKey(d => d.Payload, new JsonKey("tier"), descending: true, nullsFirst: true).RenderSelect());
    }

    [Fact]
    public void The_nulls_position_is_independent_of_the_direction_which_is_why_it_is_stated()
    {
        Assert.Contains(
            "ORDER BY (\"payload\" ->> @p0) ASC NULLS FIRST",
            Query().OrderByJsonKey(d => d.Payload, new JsonKey("tier"), descending: false, nullsFirst: true).RenderSelect());

        Assert.Contains(
            "ORDER BY (\"payload\" ->> @p0) DESC NULLS LAST",
            Query().OrderByJsonKey(d => d.Payload, new JsonKey("tier"), descending: true, nullsFirst: false).RenderSelect());
    }

    [Fact]
    public void ThenByJsonKey_appends_a_second_term_with_its_own_key_binding()
    {
        var sql = Query()
            .OrderByJsonKey(d => d.Payload, new JsonKey("tier"), descending: false, nullsFirst: false)
            .ThenByJsonKey(d => d.Payload, new JsonKey("rank"), descending: true, nullsFirst: true)
            .RenderSelect();

        Assert.Contains(
            "ORDER BY (\"payload\" ->> @p0) ASC NULLS LAST, (\"payload\" ->> @p1) DESC NULLS FIRST",
            sql);
    }

    [Fact]
    public void Rendering_the_same_query_TWICE_binds_the_key_once_because_the_term_is_built_where_it_is_added()
    {
        var query = Query()
            .Where(d => Db.JsonKeyEquals(d.Payload, "tier", "gold", JsonValueOrder.Text))
            .OrderByJsonKey(d => d.Payload, new JsonKey("rank"), descending: false, nullsFirst: false);

        Assert.Equal(query.RenderSelect(), query.RenderSelect());
        Assert.Contains("@p2", query.RenderSelect());
        Assert.DoesNotContain("@p3", query.RenderSelect());
    }

    [Fact]
    public void The_six_pre_existing_order_forms_render_with_the_stated_NULLS_LAST_and_nothing_else_moved()
    {
        Assert.Contains("ORDER BY \"label\" ASC", Query().OrderBy(d => d.Label).RenderSelect());
        Assert.Contains("ORDER BY \"label\" DESC", Query().OrderByDescending(d => d.Label).RenderSelect());
        Assert.Contains(
            "ORDER BY \"label\" ASC NULLS LAST, \"id\" ASC NULLS LAST",
            Query().OrderBy(d => d.Label).ThenBy(d => d.Id).RenderSelect());
        Assert.Contains(
            "ORDER BY \"label\" ASC NULLS LAST, \"id\" DESC NULLS LAST",
            Query().OrderBy(d => d.Label).ThenByDescending(d => d.Id).RenderSelect());
        Assert.Contains(
            "ORDER BY (\"label\" = @p0) ASC NULLS LAST",
            Query().OrderByPredicate(d => d.Label == "x").RenderSelect());
        Assert.Contains(
            "ORDER BY \"id\" ASC NULLS LAST, (\"label\" = @p0) ASC NULLS LAST",
            Query().OrderBy(d => d.Id).ThenByPredicate(d => d.Label == "x").RenderSelect());
    }

    [Fact]
    public void The_two_secondary_forms_differ_from_each_other_so_neither_can_stand_in_for_the_other()
    {
        Assert.NotEqual(
            Query().OrderBy(d => d.Label).ThenBy(d => d.Id).RenderSelect(),
            Query().OrderBy(d => d.Label).ThenByDescending(d => d.Id).RenderSelect());
    }

    [Fact]
    public void OrderBy_still_CLEARS_earlier_terms_while_ThenBy_still_appends()
    {
        var sql = Query()
            .OrderByJsonKey(d => d.Payload, new JsonKey("first"), descending: false, nullsFirst: false)
            .OrderBy(d => d.Label)
            .RenderSelect();

        Assert.Contains("ORDER BY \"label\" ASC", sql);
        Assert.DoesNotContain("->>", sql);
    }

    [Fact]
    public void A_NULL_document_now_renders_DIFFERENTLY_from_a_real_one_where_it_used_to_render_the_same()
    {
        var withDocument = Query().RenderUpsert(
            RowCarryingADocument(), [d => d.Id], OnConflict.DoNothing).Sql;

        var withNull = Query().RenderUpsert(
            new Row<FixtureDocument>()
                .With(d => d.Id, Guid.NewGuid())
                .With(d => d.Payload, null)
                .With(d => d.Label, "a plain string neighbour"),
            [d => d.Id], OnConflict.DoNothing).Sql;

        Assert.NotEqual(withDocument, withNull);
        Assert.Contains("::jsonb", withDocument);
        Assert.DoesNotContain("::jsonb", withNull);
    }

    [Fact]
    public void An_OMITTED_column_names_no_placeholder_for_it_at_all()
    {
        var (sql, parameters) = Query().RenderUpsert(
            new Row<FixtureDocument>().With(d => d.Id, Guid.NewGuid()),
            [d => d.Id], OnConflict.DoNothing);

        Assert.DoesNotContain("\"payload\"", sql);
        Assert.Single(parameters);
    }

    private static void AssertRefusedByTheBuildersGuard(NotSupportedException refusal)
    {
        Assert.Contains("SqliteDialect", refusal.Message);
        Assert.Contains("a statement emitted anyway", refusal.Message);
        Assert.DoesNotContain("swap-readiness", refusal.Message);
    }

    [Fact]
    public void A_dialect_with_no_JSON_document_type_refuses_the_bind_BY_NAME()
    {
        var refusal = Assert.Throws<NotSupportedException>(
            () => On(Sqlite()).Set(d => d.Payload, Document()));

        Assert.Contains("JSON document", refusal.Message);
        AssertRefusedByTheBuildersGuard(refusal);
    }

    [Fact]
    public void That_same_dialect_still_binds_every_value_that_is_not_a_document()
    {
        var sql = On(Sqlite())
            .Set(d => d.Label, "an ordinary string")
            .Where(d => d.Label == "any")
            .RenderUpdate();

        Assert.Contains("SET \"label\" = @p0", sql);
    }

    [Fact]
    public void InsertMany_is_the_write_shape_that_REACHES_the_JSON_guard_on_a_dialect_without_documents()
    {
        AssertRefusedByTheBuildersGuard(Assert.Throws<NotSupportedException>(
            () => On(Sqlite()).RenderInsertMany(new[] { RowCarryingADocument() })));
    }

    [Fact]
    public void The_UPSERT_door_is_closed_EARLIER_on_this_fixture_and_by_a_different_clause()
    {
        var values = RowCarryingADocument();

        foreach (var refusal in new[]
        {
            Assert.Throws<NotSupportedException>(
                () => On(Sqlite()).RenderUpsert(values, [d => d.Id], OnConflict.DoNothing)),
            Assert.Throws<NotSupportedException>(
                () => On(Sqlite()).RenderUpsert(
                    values, [d => d.Id], OnConflict.DoUpdate,
                    sets: [UpsertSet<FixtureDocument>.Inserted(d => d.Payload)])),
        })
        {
            Assert.Contains("has no upsert", refusal.Message);
            Assert.DoesNotContain("JSON document", refusal.Message);
        }
    }

    [Fact]
    public void A_dialect_with_no_JSON_document_type_refuses_both_key_predicates_and_the_ordering()
    {
        var document = Document();

        AssertRefusedByTheBuildersGuard(Assert.Throws<NotSupportedException>(
            () => On(Sqlite()).Where(d => Db.JsonKeyEquals(d.Payload, "tier", "gold", JsonValueOrder.Text))));

        AssertRefusedByTheBuildersGuard(Assert.Throws<NotSupportedException>(
            () => On(Sqlite()).Where(d => Db.JsonHasKey(d.Payload, "tier"))));

        AssertRefusedByTheBuildersGuard(Assert.Throws<NotSupportedException>(
            () => On(Sqlite()).OrderByJsonKey(d => d.Payload, new JsonKey("tier"), descending: false, nullsFirst: false)));

        AssertRefusedByTheBuildersGuard(Assert.Throws<NotSupportedException>(
            () => On(Sqlite()).ThenByJsonKey(d => d.Payload, new JsonKey("tier"), descending: false, nullsFirst: false)));

        AssertRefusedByTheBuildersGuard(Assert.Throws<NotSupportedException>(
            () => On(Sqlite()).Where(d => d.Payload == document)));
    }

    [Fact]
    public void Comparing_the_key_to_a_null_value_is_refused_and_the_refusal_names_the_operator_that_answers_it()
    {
        string? absent = null;

        var refusal = Assert.Throws<ArgumentNullException>(
            () => Query().Where(d => Db.JsonKeyEquals(d.Payload, "tier", absent!, JsonValueOrder.Text)));

        Assert.Contains("Db.JsonHasKey", refusal.Message);
    }

    [Fact]
    public void A_null_key_is_refused_by_the_predicate_and_by_the_ordering_alike()
    {
        string? absent = null;

        Assert.Throws<ArgumentNullException>(
            () => Query().Where(d => Db.JsonHasKey(d.Payload, absent!)));

        Assert.Throws<ArgumentNullException>(
            () => Query().OrderByJsonKey(d => d.Payload, new JsonKey(absent!), descending: false, nullsFirst: false));
    }

    [Fact]
    public void The_key_predicate_refuses_a_first_argument_that_is_not_a_column_of_the_entity()
    {
        var loose = Document();

        Assert.Contains(
            "must be a column",
            Assert.Throws<NotSupportedException>(
                () => Query().Where(d => Db.JsonKeyEquals(loose, "tier", "gold", JsonValueOrder.Text))).Message);

        Assert.Contains(
            "must be a column",
            Assert.Throws<NotSupportedException>(
                () => Query().Where(d => Db.JsonHasKey(loose, "tier"))).Message);
    }
}
