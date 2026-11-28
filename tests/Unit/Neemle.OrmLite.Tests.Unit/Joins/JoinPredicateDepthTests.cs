using System.Linq.Expressions;
using Xunit;

namespace Neemle.OrmLite.Tests.Unit.Joins;

// ORML-151 F2: compiling and rendering a join predicate recurse once per nesting level, so the depth has a bound of its
// own (128 levels) that holds whatever MaxPredicateNodes allows, and a deeper tree is refused with LimitExceeded
// before any SQL is built or any connection is opened.
public sealed class JoinPredicateDepthTests
{
    private static readonly JoinLimits Unbounded = new(4, 8, int.MaxValue, 64, 16);

    private readonly FakeJoinDatabase<JoinPredicateDepthTests> _db = new();

    // A predicate `depth` levels deep: the seed is one level, and each And adds one above it.
    private static JoinPredicate Nested(JoinSource<UnitJoinRow<JoinPredicateDepthTests>> root,
        JoinSource<UnitJoinRow<JoinPredicateDepthTests>> right, int depth)
    {
        var predicate = root.Column(x => x.Id).Equal(right.Column(x => x.Id));
        for (var level = 1; level < depth; level++)
            predicate = predicate.And(root.Column(x => x.Name).Equal(right.Column(x => x.Name)));
        return predicate;
    }

    private void NothingRan()
    {
        Assert.Equal(0, _db.Probe.Connections);
        Assert.Equal(0, _db.Probe.Commands);
    }

    [Fact(DisplayName = "an ON predicate 128 levels deep is accepted under an unbounded node limit")]
    public void An_on_predicate_at_the_depth_bound_is_accepted()
    {
        var (root, right) = (_db.Root(), _db.Right());

        var query = Joined.From(root, Unbounded).InnerJoin(right, Nested(root, right, 128));

        Assert.NotNull(query);
        NothingRan();
    }

    [Fact(DisplayName = "an ON predicate 129 levels deep is refused with LimitExceeded under an unbounded node limit")]
    public void An_on_predicate_past_the_depth_bound_is_refused()
    {
        var (root, right) = (_db.Root(), _db.Right());
        var on = Nested(root, right, 129);

        var error = Assert.Throws<JoinReadException>(() => Joined.From(root, Unbounded).InnerJoin(right, on));

        Assert.Equal(JoinError.LimitExceeded, error.Error);
        NothingRan();
    }

    [Fact(DisplayName = "a result WHERE 129 levels deep is refused with LimitExceeded before any connection opens")]
    public void A_result_where_past_the_depth_bound_is_refused_before_io()
    {
        var (root, right) = (_db.Root(), _db.Right());
        var query = Joined.From(root, Unbounded).InnerJoin(right, Nested(root, right, 1))
            .Where(Nested(root, right, 129));
        var selection = new JoinSelection();
        selection.Add(root.Column(x => x.Name), "name");

        // Refused while the read is compiled, synchronously, so no task is ever started.
        var error = Assert.Throws<JoinReadException>(() => { _ = query.ToListAsync(selection); });

        Assert.Equal(JoinError.LimitExceeded, error.Error);
        NothingRan();
    }

    [Theory(DisplayName = "a captured root WHERE is bounded by the same depth")]
    [InlineData(128, false)]
    [InlineData(129, true)]
    public void A_captured_source_where_is_bounded_by_the_same_depth(int depth, bool refused)
    {
        var root = _db.Query().Where(OrElse(depth)).AsJoinSource("root");
        var right = _db.Right();
        var on = root.Column(x => x.Id).Equal(right.Column(x => x.Id));

        var error = Record.Exception(() => Joined.From(root, Unbounded).InnerJoin(right, on));

        if (refused) Assert.Equal(JoinError.LimitExceeded, Assert.IsType<JoinReadException>(error).Error);
        else Assert.Null(error);
        NothingRan();
    }

    // The right source's captured filters are ANDed onto the ON one level each when the join is rendered, so the bound
    // holds for that folded tree: a one-level ON under 127 captured filters is 128 deep, under 128 it is 129 (§803 F1).
    [Theory(DisplayName = "the right source's captured filters count toward the depth of the ON they are folded into")]
    [InlineData(127, false)]
    [InlineData(128, true)]
    public void Captured_right_filters_count_toward_the_folded_on(int filters, bool refused)
    {
        var root = _db.Root();
        var query = _db.Query();
        for (var i = 0; i < filters; i++) query = query.Where(x => x.Name != "");
        var right = query.AsJoinSource("right");
        var on = root.Column(x => x.Id).Equal(right.Column(x => x.Id));

        var error = Record.Exception(() => Joined.From(root, Unbounded).InnerJoin(right, on));

        if (refused) Assert.Equal(JoinError.LimitExceeded, Assert.IsType<JoinReadException>(error).Error);
        else Assert.Null(error);
        NothingRan();
    }

    // x => x.Name == "0" || x.Name == "1" || ...: `depth` comparisons, an Or above each but the first.
    private static Expression<Func<UnitJoinRow<JoinPredicateDepthTests>, bool>> OrElse(int depth)
    {
        var row = Expression.Parameter(typeof(UnitJoinRow<JoinPredicateDepthTests>), "x");
        var name = Expression.Property(row, nameof(UnitJoinRow<JoinPredicateDepthTests>.Name));
        Expression body = Expression.Equal(name, Expression.Constant("0"));
        for (var i = 1; i < depth; i++)
            body = Expression.OrElse(body, Expression.Equal(name, Expression.Constant(i.ToString())));
        return Expression.Lambda<Func<UnitJoinRow<JoinPredicateDepthTests>, bool>>(body, row);
    }
}
