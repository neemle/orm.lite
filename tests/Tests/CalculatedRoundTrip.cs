using Xunit;

namespace Neemle.OrmLite.Tests;

internal static class CalculatedRoundTrip
{
    internal static async Task VerifyAsync(OrmContext db)
    {
        var id = Guid.NewGuid();
        await Assert.IsAssignableFrom<Task>(CalculatedFieldTests.Call("Add", db, id, "MiXeD"));
        Assert.Equal("mixed", await ReadAsync(db, id));
        var list = await Assert.IsAssignableFrom<Task<string?[]>>(CalculatedFieldTests.Call("List", db));
        Assert.Equal(new string?[] { "mixed" }, list);
        var graph = await Assert.IsAssignableFrom<Task<string?[]>>(CalculatedFieldTests.Call("Relations", db, id));
        Assert.Equal(new string?[] { "mixed", "child", "tag" }, graph);
        await Assert.IsAssignableFrom<Task>(CalculatedFieldTests.Call("Change", db, id));
        Assert.Equal("changed", await ReadAsync(db, id));
        await Assert.IsAssignableFrom<Task>(CalculatedFieldTests.Call("Add", db, Guid.NewGuid(), null));
        var nullable = await Assert.IsAssignableFrom<Task<string?[]>>(CalculatedFieldTests.Call("List", db));
        Assert.Contains(nullable, value => value is null);
        Assert.Null(await ReadAsync(db, Guid.NewGuid()));
    }

    private static Task<string?> ReadAsync(OrmContext db, Guid id) =>
        Assert.IsAssignableFrom<Task<string?>>(CalculatedFieldTests.Call("Key", db, id));
}
