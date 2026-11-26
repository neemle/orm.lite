namespace ForeignKeysDemo;

internal static class Results
{
    private static readonly SortedDictionary<string, string> Rows = new(StringComparer.Ordinal);
    internal static int Assertions { get; private set; }
    internal static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Assertions++;
    }
    internal static void Case(string name, string value) => Rows.Add(name, value);
    internal static void Print()
    {
        Console.WriteLine("FOREIGN-KEY-DEMO v1");
        foreach (var row in Rows) Console.WriteLine(row.Key + "=" + row.Value);
        Console.WriteLine("assertions=" + Assertions + " PASS");
    }
}
