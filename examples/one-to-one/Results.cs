namespace OneToOneDemo;

internal static class Results
{
    private static int _assertions;
    private static readonly SortedDictionary<string, string> Cases = new(StringComparer.Ordinal);
    internal static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        _assertions++;
    }
    internal static void Case(string name, string value) => Cases.Add(name, value);
    internal static void Print()
    {
        Console.WriteLine("ONE-TO-ONE-DEMO v1");
        foreach (var item in Cases) Console.WriteLine(item.Key + "=" + item.Value);
        Console.WriteLine("assertions=" + _assertions + " PASS");
    }
}
