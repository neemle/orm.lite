namespace Neemle.OrmLite.Sqlite;

internal static class SqliteFileIdentity
{
    private const int MaxLinkHops = 40;

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    internal static string Canonical(string fullPath)
    {
        var resolved = ResolveLinks(fullPath);
        return Fold(resolved, IsCaseInsensitive(resolved));
    }

    internal static string Fold(string path, bool insensitive) => insensitive ? path.ToLowerInvariant() : path;

    private static string ResolveLinks(string fullPath)
    {
        var current = Path.GetPathRoot(fullPath) ?? string.Empty;
        var pending = new Stack<string>();
        PushSegments(pending, fullPath[current.Length..]);
        var hops = 0;
        while (pending.TryPop(out var segment))
        {
            if (segment == "..") current = Path.GetDirectoryName(current) ?? current;
            else if (segment != ".") current = Enter(current, segment, pending, ref hops);
            if (hops > MaxLinkHops) return fullPath;
        }
        return current;
    }

    private static string Enter(string current, string segment, Stack<string> pending, ref int hops)
    {
        var next = Path.Combine(current, segment);
        var target = LinkTarget(next);
        if (target is null) return next;
        hops++;
        if (!Path.IsPathRooted(target))
        {
            PushSegments(pending, target);
            return current;
        }
        var root = Path.GetPathRoot(target) ?? string.Empty;
        PushSegments(pending, target[root.Length..]);
        return root;
    }

    private static void PushSegments(Stack<string> pending, string path)
    {
        var parts = path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        for (var i = parts.Length - 1; i >= 0; i--) pending.Push(parts[i]);
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return info.LinkTarget;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsCaseInsensitive(string path)
    {
        for (var entry = path; !string.IsNullOrEmpty(entry); entry = Path.GetDirectoryName(entry))
        {
            var name = Path.GetFileName(entry);
            var flipped = Flip(name);
            if (name.Length == 0 || flipped == name || !Path.Exists(entry)) continue;
            return Aliases(entry, name, flipped);
        }
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
    }

    private static bool Aliases(string entry, string name, string flipped)
    {
        var parent = Path.GetDirectoryName(entry);
        if (parent is null || !Path.Exists(Path.Combine(parent, flipped))) return false;
        try
        {
            return Directory.EnumerateFileSystemEntries(parent)
                .Count(found => Path.GetFileName(found).Equals(name, StringComparison.OrdinalIgnoreCase)) == 1;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static string Flip(string name)
    {
        var upper = name.ToUpperInvariant();
        return upper != name ? upper : name.ToLowerInvariant();
    }
}
