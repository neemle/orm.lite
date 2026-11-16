using System;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class DocumentComparisonTests
{
    private static Jsonb J(string json) => Jsonb.Parse(json);

    [Fact]
    public void Reordered_keys_and_the_servers_own_spacing_are_the_same_document_and_not_the_same_text()
    {
        var written = J("{\"b\":1,\"a\":[2,3],\"nested\":{\"x\":true}}");
        var readBack = J("{\"a\": [2, 3], \"b\": 1, \"nested\": {\"x\": true}}");

        Assert.True(Jsonb.SameDocument(written, readBack));
        Assert.False(written.Equals(readBack));
    }

    [Fact]
    public void A_duplicate_key_collapses_to_its_LAST_value_the_way_the_server_stores_it()
    {
        Assert.True(Jsonb.SameDocument(J("{\"dup\":1,\"dup\":2}"), J("{\"dup\": 2}")));
        Assert.False(Jsonb.SameDocument(J("{\"dup\":1,\"dup\":2}"), J("{\"dup\": 1}")));
    }

    [Fact]
    public void A_number_compares_by_value_because_the_server_answered_TRUE_for_one_point_zero_and_one()
    {
        var scaled = J("{\"n\":1.0}");
        var bare = J("{\"n\":1}");

        Assert.True(Jsonb.SameDocument(scaled, bare));
        Assert.False(scaled.Equals(bare));
    }

    [Fact]
    public void Two_long_integers_differing_by_ONE_are_different_documents()
    {
        Assert.False(Jsonb.SameDocument(
            J("{\"n\":123456789012345678901234567890123}"),
            J("{\"n\":123456789012345678901234567890124}")));
    }

    [Fact]
    public void Two_magnitudes_too_large_for_a_double_are_different_documents_and_not_one_infinity()
    {
        Assert.False(Jsonb.SameDocument(J("{\"n\":1e400}"), J("{\"n\":1e500}")));
    }

    [Fact]
    public void Two_values_differing_in_the_thirty_first_decimal_place_are_different_documents()
    {
        Assert.False(Jsonb.SameDocument(
            J("{\"n\":0.1234567890123456789012345678901}"),
            J("{\"n\":0.1234567890123456789012345678902}")));
    }

    [Fact]
    public void Scale_insensitivity_does_NOT_stop_where_a_double_gives_up_which_is_why_text_is_not_the_remedy()
    {
        Assert.True(Jsonb.SameDocument(J("{\"n\":1.0e400}"), J("{\"n\":1e400}")));
        Assert.True(Jsonb.SameDocument(J("{\"n\":1e2}"), J("{\"n\":100}")));
        Assert.True(Jsonb.SameDocument(J("{\"n\":1.00}"), J("{\"n\":1}")));
    }

    [Fact]
    public void A_value_below_one_supplies_its_own_leading_zero_and_still_matches_the_exponent_form()
    {
        Assert.True(Jsonb.SameDocument(J("{\"n\":0.5}"), J("{\"n\":5e-1}")));
        Assert.True(Jsonb.SameDocument(J("{\"n\":0.05}"), J("{\"n\":5e-2}")));
        Assert.False(Jsonb.SameDocument(J("{\"n\":0.5}"), J("{\"n\":0.6}")));
    }

    [Fact]
    public void Every_spelling_of_zero_is_one_document_including_the_signed_one()
    {
        Assert.True(Jsonb.SameDocument(J("{\"n\":-0}"), J("{\"n\":0}")));
        Assert.True(Jsonb.SameDocument(J("{\"n\":0.0}"), J("{\"n\":0}")));
        Assert.True(Jsonb.SameDocument(J("{\"n\":0e10}"), J("{\"n\":0}")));
        Assert.False(Jsonb.SameDocument(J("{\"n\":0}"), J("{\"n\":1}")));
    }

    [Fact]
    public void A_sign_is_content_everywhere_except_on_zero()
    {
        Assert.False(Jsonb.SameDocument(J("{\"n\":-1}"), J("{\"n\":1}")));
        Assert.False(Jsonb.SameDocument(J("{\"n\":-1e400}"), J("{\"n\":1e400}")));
    }

    [Fact]
    public void An_exponent_too_large_to_hold_is_decided_by_TEXT_and_that_branch_is_REACHABLE()
    {
        var huge = "{\"n\":1e99999999999999999999}";

        Assert.True(Jsonb.SameDocument(J(huge), J(huge)));
        Assert.False(Jsonb.SameDocument(J(huge), J("{\"n\":1.0e99999999999999999999}")));
        Assert.False(Jsonb.SameDocument(J(huge), J("{\"n\":1e99999999999999999998}")));
    }

    [Fact]
    public void An_ARRAY_is_ordered_and_that_is_the_one_place_JSON_order_is_content()
    {
        Assert.False(Jsonb.SameDocument(J("{\"a\":[1,2]}"), J("{\"a\":[2,1]}")));
        Assert.True(Jsonb.SameDocument(J("{\"a\":[1,2]}"), J("{\"a\":[1, 2]}")));
    }

    [Fact]
    public void A_genuinely_different_document_is_different_under_BOTH_comparisons()
    {
        var one = J("{\"tier\":\"gold\"}");
        var other = J("{\"tier\":\"silver\"}");

        Assert.False(Jsonb.SameDocument(one, other));
        Assert.False(one.Equals(other));
    }

    [Fact]
    public void A_MISSING_key_and_a_key_whose_value_is_JSON_null_are_NOT_the_same_document()
    {
        Assert.False(Jsonb.SameDocument(J("{}"), J("{\"k\":null}")));
        Assert.True(Jsonb.SameDocument(J("{\"k\":null}"), J("{\"k\": null}")));
    }

    [Fact]
    public void An_EXTRA_key_is_a_difference_in_whichever_order_the_two_arrive()
    {
        Assert.False(Jsonb.SameDocument(J("{\"a\":1}"), J("{\"a\":1,\"b\":2}")));
        Assert.False(Jsonb.SameDocument(J("{\"a\":1,\"b\":2}"), J("{\"a\":1}")));
    }

    [Fact]
    public void A_number_and_the_STRING_of_that_number_are_different_documents()
    {
        Assert.False(Jsonb.SameDocument(J("{\"n\":1}"), J("{\"n\":\"1\"}")));
    }

    [Fact]
    public void Two_absent_documents_are_the_same_and_one_absent_document_is_not()
    {
        Assert.True(Jsonb.SameDocument(null, null));
        Assert.False(Jsonb.SameDocument(null, J("{}")));
        Assert.False(Jsonb.SameDocument(J("{}"), null));
    }

    [Fact]
    public void The_ordinal_pair_pinned_still_answers_exactly_as_it_did()
    {
        var one = J("{\"a\":1}");
        var same = J("{\"a\":1}");
        var spaced = J("{\"a\": 1}");

        Assert.True(one.Equals(same));
        Assert.Equal(one.GetHashCode(), same.GetHashCode());
        Assert.False(one.Equals(spaced));
        Assert.True(Jsonb.SameDocument(one, spaced));
    }
}
