namespace CalculatedDemo;

internal static class Results
{
    private static readonly SortedDictionary<string, string> Cases = new(StringComparer.Ordinal);
    internal static int Count { get; private set; }
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Count++;
    }
    internal static void Case(string name, string value) => Cases.Add(name, value);
    internal static string Text(string? value) => value is null ? "<null>" : value.Length == 0 ? "<empty>" : value;
    internal static void Print()
    {
        Console.WriteLine("CALCULATED-DEMO v1");
        foreach (var item in Cases) Console.WriteLine(item.Key + "=" + item.Value);
        Console.WriteLine("assertions=" + Count + " PASS");
    }
}
