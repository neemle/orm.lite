using System.Reflection;

namespace FoundationProof;

internal static class NativeApiProof
{
    internal static void Show()
    {
        foreach (var method in typeof(SQLitePCL.raw).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name.Contains("NativeLibrary", StringComparison.Ordinal)
                || m.Name.Contains("Provider", StringComparison.Ordinal)))
            Console.WriteLine(method);
        Console.WriteLine("handle base=" + typeof(SQLitePCL.sqlite3).BaseType);
    }
}
