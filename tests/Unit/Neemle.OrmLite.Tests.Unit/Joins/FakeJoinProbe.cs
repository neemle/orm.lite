namespace Neemle.OrmLite.Tests.Unit.Joins;

// What the fake driver saw, and the rows its reader returns. AfterRead runs after each Read with its 1-based number
// and whether a row was available.
internal sealed class FakeJoinProbe
{
    internal string[] Headers = ["__orm_join_0", "__orm_join_1", "__orm_join_2"];
    internal object?[][] Rows = [];
    internal int Connections, Opens, Commands, Reads;
    internal Action? Opening;
    internal Action<int, bool>? AfterRead;
}
