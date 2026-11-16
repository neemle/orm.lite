using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class RegistrationOwnershipTests
{
    private sealed class OwnershipFixture { }

    private const string Table = "ownership_fixture";

    private static readonly string[] FixtureColumns = { "id", "name", "rank" };

    private static string Col(string member) => member switch
    {
        "Id" => "id",
        "Name" => "name",
        "Rank" => "rank",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown fixture member."),
    };

    [Fact(DisplayName = "neither published view of a registration accepts an element write, where the live string[] both views used to hand out accepted it silently and process-wide")]
    public void Both_published_views_refuse_the_element_write_a_live_array_would_have_accepted()
    {
        OrmMeta.Register<OwnershipFixture>(Table, Col, FixtureColumns);

        IList<string> typeKeyed = OrmMeta.Get(typeof(OwnershipFixture)).Columns;
        IList<string> tableKeyed = OrmMeta.ColumnsForTable(Table)!.Value;

        Assert.Throws<NotSupportedException>(() => typeKeyed[0] = "hijacked");
        Assert.Throws<NotSupportedException>(() => tableKeyed[0] = "hijacked");

        Assert.Throws<NotSupportedException>(() => typeKeyed.Add("smuggled"));
        Assert.Throws<NotSupportedException>(() => tableKeyed.Clear());

        var after = OrmMeta.ColumnsForTable(Table)!.Value;
        Assert.Equal(FixtureColumns.Length, after.Length);
        Assert.Equal("id", after[0]);
    }

    [Fact(DisplayName = "Register does not keep the caller's buffer, so an array a caller registers and then writes to afterwards leaves the registration untouched")]
    public void Register_does_not_keep_the_callers_buffer_so_a_later_write_by_the_caller_changes_nothing()
    {
        var callerOwned = new[] { "id", "name", "rank" };
        OrmMeta.Register<OwnershipFixture>(Table, Col, callerOwned);

        callerOwned[^1] = null!;
        callerOwned[0] = "hijacked";

        IList<string> registered = OrmMeta.Get(typeof(OwnershipFixture)).Columns;

        Assert.Equal(3, registered.Count);
        Assert.Equal("id", registered[0]);
        Assert.Equal("rank", registered[^1]);

        OrmMeta.Register<OwnershipFixture>(Table, Col, FixtureColumns);
    }

    [Fact(DisplayName = "the column set is PUBLISHED as an immutable collection type on both views and Register takes a span, so the refusals above are properties of the signatures and not of one call path")]
    public void The_published_signatures_are_what_make_the_registration_unwritable()
    {
        var get = typeof(OrmMeta).GetMethod(nameof(OrmMeta.Get))!;

        Assert.Equal(
            typeof(ImmutableArray<string>),
            get.ReturnType.GetGenericArguments()[2]);

        Assert.Equal(
            typeof(ImmutableArray<string>?),
            typeof(OrmMeta).GetMethod(nameof(OrmMeta.ColumnsForTable))!.ReturnType);

        Assert.False(
            typeof(ImmutableArray<string>).GetProperty("Item")?.CanWrite ?? false,
            "the published column set's indexer must stay read-only; a settable one re-opens the "
            + "in-place rewrite of a registration that the registration-ownership change closed.");

        Assert.Equal(
            typeof(ReadOnlySpan<string>),
            typeof(OrmMeta).GetMethod(nameof(OrmMeta.Register))!.GetParameters()[2].ParameterType);
    }

    [Fact(DisplayName = "the two views are still ONE registration after the type change, and a copy holding the same names is not mistaken for one")]
    public void The_two_views_remain_a_second_key_onto_one_registration()
    {
        OrmMeta.Register<OwnershipFixture>(Table, Col, FixtureColumns);

        var typeKeyed = OrmMeta.Get(typeof(OwnershipFixture)).Columns;
        var tableKeyed = OrmMeta.ColumnsForTable(Table)!.Value;

        Assert.NotEmpty(typeKeyed);

        Assert.True(
            typeKeyed == tableKeyed,
            "the type-keyed and table-keyed views are no longer ONE registration. ImmutableArray's == "
            + "is reference equality of the backing array, so this asks what Assert.Same asked while "
            + "the column set was a string[]: Register writes one value into both maps and they "
            + "cannot drift apart.");

        Assert.False(
            typeKeyed == ImmutableArray.Create(typeKeyed.AsSpan()),
            "the assertion above is VACUOUS: == matched a COPY holding the same names, so it is "
            + "comparing sequences rather than backing arrays and would stay green after the two "
            + "views drifted onto separate registrations.");
    }

    [Fact(DisplayName = "a table nothing registered still answers ABSENT rather than an empty column set, which the nullable published type is what preserves")]
    public void An_unregistered_table_still_answers_absent_rather_than_empty()
    {
        Assert.False(
            OrmMeta.ColumnsForTable("ownership_no_such_table").HasValue,
            "OrmMeta answered a table nothing ever registered. ABSENT and EMPTY are different answers "
            + "here and only the caller knows which of them is fatal for it; a default ImmutableArray "
            + "or an Empty one in place of null would collapse that distinction.");
    }
}
