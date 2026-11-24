using System.Globalization;

namespace Neemle.Orm.Lite.Tests.Ports;

// The recorded coupling of the provider-neutral sources. One line per file and marker, "<file>|<marker>|<count>"; a
// line starting with # is a comment. The list may only shrink: the ratchet fails on a higher count, on a new entry,
// and on a count that is lower than the line says, so that the line is lowered in the same change.
internal static class CoreCouplingAllowList
{
    internal const string RelativePath = "tests/Neemle.Orm.Lite.Tests/Ports/core-coupling-allowlist.txt";

    internal static SortedDictionary<string, int> Read(string root)
    {
        var entries = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var path = Path.Combine(root, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0 || lines[i][0] == '#') continue;
            var (key, count) = Parse(lines[i], i + 1);
            if (!entries.TryAdd(key, count)) throw new InvalidDataException("line " + (i + 1) + " repeats " + key);
        }
        return entries;
    }

    private static (string Key, int Count) Parse(string line, int number)
    {
        var parts = line.Split('|');
        var count = 0;
        if (parts.Length != 3 || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out count))
            throw new InvalidDataException("line " + number + " is not <file>|<marker>|<count>: " + line);
        return (parts[0] + "|" + parts[1], count);
    }
}
