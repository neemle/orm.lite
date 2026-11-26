namespace OneToOneDemo;

internal static class MappingFaults
{
    internal static readonly InvalidOperationException Primary = new("controlled mapping failure");
    internal static bool Enabled { get; set; }
    internal static string Read(string value)
    {
        if (Enabled) throw Primary;
        return value;
    }
}
