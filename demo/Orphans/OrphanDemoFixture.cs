using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal sealed class OrphanDemoFixture
{
    internal OrphanDemoParent Parent { get; } = new() { Id = Guid.NewGuid(), Name = "owner" };
    internal OrphanDemoChild First { get; private set; } = null!;
    internal OrphanDemoChild Second { get; private set; } = null!;
    internal static async Task<OrphanDemoFixture> SeedAsync(OrmContext db)
    {
        var f = new OrphanDemoFixture();
        f.First = new() { Id = Guid.NewGuid(), ParentId = f.Parent.Id, Name = "first" };
        f.Second = new() { Id = Guid.NewGuid(), ParentId = f.Parent.Id, Name = "second" };
        f.Parent.Children = [f.First, f.Second];
        await OrphanDemoParentOrm.InsertAsync(db, f.Parent);
        await OrphanDemoChildOrm.InsertAsync(db, f.First);
        await OrphanDemoChildOrm.InsertAsync(db, f.Second);
        return f;
    }
    internal async Task UnchangedAsync(OrmContext db)
    {
        var first = await OrphanDemoChildOrm.GetAsync(db, First.Id);
        var second = await OrphanDemoChildOrm.GetAsync(db, Second.Id);
        OrphanDemoChecks.Require(first?.ParentId == Parent.Id && second?.ParentId == Parent.Id, "membership unchanged");
        OrphanDemoChecks.Require(first?.Name == "first" && second?.Name == "second", "payload unchanged");
        InputsUnchanged();
    }
    internal void InputsUnchanged() => OrphanDemoChecks.Require(First.ParentId == Parent.Id
        && Second.ParentId == Parent.Id && Parent.Children.Count == 2
        && ReferenceEquals(Parent.Children[0], First) && ReferenceEquals(Parent.Children[1], Second),
        "caller FKs and collection unchanged");
}
