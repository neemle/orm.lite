using Xunit;

namespace Neemle.OrmLite.Tests.Unit.Joins;

// ORML-151 F3: the joined plan is validated per execution, at its boundaries (when it is compiled, before the
// command, before the rows are published), and never per materialized row.
public sealed class JoinPlanValidationTests
{
    private static readonly JoinLimits Limits = new(4, 8, 64, 16, 16);

    private static readonly Guid First = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private sealed class Rows;
    private sealed class Opened;
    private sealed class Published;

    private static (JoinQuery Query, JoinSelection Selection, JoinSlot<string> Name) Left<TMarker>(
        FakeJoinDatabase<TMarker> db, int rows)
    {
        db.Probe.Rows = Enumerable.Range(0, rows).Select(i => new object?[] { "root" + i, "note" + i, First }).ToArray();
        var (root, right) = (db.Root(), db.Right());
        var selection = new JoinSelection();
        var name = selection.Add(root.Column(x => x.Name), "name");
        selection.Add(right.Column(x => x.Note), "note");
        return (Joined.From(root, Limits).LeftJoin(right, root.Column(x => x.Id).Equal(right.Column(x => x.Id))),
            selection, name);
    }

    [Fact(DisplayName = "a registration replaced after the first row and restored at the end does not stop the read: no per-row validation")]
    public async Task The_plan_is_not_validated_per_row()
    {
        var db = new FakeJoinDatabase<Rows>();
        var (query, selection, name) = Left(db, 3);
        db.Probe.AfterRead = (read, available) =>
        {
            if (read == 1) OrmMeta.Register<UnitJoinRow<Rows>>(UnitJoinModel<Rows>.Create());
            if (!available) UnitJoinModel<Rows>.Restore();
        };
        try
        {
            var rows = await query.ToListAsync(selection);

            Assert.Equal(new[] { "root0", "root1", "root2" }, rows.Select(r => r.Get(name).Value));
            Assert.Equal(4, db.Probe.Reads);
        }
        finally { UnitJoinModel<Rows>.Restore(); }
    }

    [Fact(DisplayName = "a registration replaced while the connection opens is refused before the command")]
    public async Task The_plan_is_validated_before_the_command()
    {
        var db = new FakeJoinDatabase<Opened>();
        var (query, selection, _) = Left(db, 1);
        db.Probe.Opening = () => OrmMeta.Register<UnitJoinRow<Opened>>(UnitJoinModel<Opened>.Create());
        try
        {
            var error = await Assert.ThrowsAsync<JoinReadException>(() => query.ToListAsync(selection));

            Assert.Equal(JoinError.UnsupportedSource, error.Error);
            Assert.Equal(1, db.Probe.Opens);
            Assert.Equal(0, db.Probe.Commands);
        }
        finally { UnitJoinModel<Opened>.Restore(); }
    }

    [Fact(DisplayName = "a registration replaced at the end of the rows is refused before the rows are published")]
    public async Task The_plan_is_validated_before_the_rows_are_published()
    {
        var db = new FakeJoinDatabase<Published>();
        var (query, selection, _) = Left(db, 2);
        db.Probe.AfterRead = (_, available) =>
        {
            if (!available) OrmMeta.Register<UnitJoinRow<Published>>(UnitJoinModel<Published>.Create());
        };
        try
        {
            var error = await Assert.ThrowsAsync<JoinReadException>(() => query.ToListAsync(selection));

            Assert.Equal(JoinError.UnsupportedSource, error.Error);
            Assert.Equal(3, db.Probe.Reads);
        }
        finally { UnitJoinModel<Published>.Restore(); }
    }
}
