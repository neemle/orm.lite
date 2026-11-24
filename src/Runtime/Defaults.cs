using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public static class Defaults
{
    public static Node Now => new ClockNow();

    public static Node EmptyDocument => new Cast(new StringLit("{}"), CastType.Json);

    public static Node Text(string value) => new StringLit(value);

    public static Node Number(long value) => new IntLit(value);

    public static Node Flag(bool value) => new BoolLit(value);
}
