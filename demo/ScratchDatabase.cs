// What this file shows
//   Housekeeping, not the library: it lets the demo run on SQLite with nothing configured. If the settings name SQLite
//   and give neither a file path nor a connection string, it creates a temporary folder, points the options at a
//   database file inside it, and deletes the folder when the demo ends. In every other case (PostgreSQL, or SQLite with a
//   Path or ConnectionString of your own) it does nothing, and your database is left alone.
//
// Library features used here
//   OrmLiteOptions: Provider ("sqlite" or "postgres"), Path (the SQLite database file) and ConnectionString are the
//       settings read from appsettings.json and OrmLite__* environment variables by OrmLiteOptions.Load(). This class
//       only fills in Path when nothing else says where the database is.
//
// How it is used (Program.cs)
//   using var scratch = ScratchDatabase.IfNoneConfigured(options);   // before OrmLite.OpenAsync
//   ... the demo runs ...
//   ok = scratch.Remove() && ok;                                       // the demo fails if the folder is still there
//   The `using` also disposes it (which calls Remove) if the demo stops early with an exception.

using Neemle.Orm.Lite;

namespace OrmLiteDemo;

internal sealed class ScratchDatabase : IDisposable
{
    // The temporary folder, or null when nothing was created.
    private readonly string? folder;

    private ScratchDatabase(string? folder) => this.folder = folder;

    public static ScratchDatabase IfNoneConfigured(OrmLiteOptions options)
    {
        // A scratch file is needed only for SQLite with no Path and no ConnectionString.
        var needsFile = string.Equals(options.Provider, "sqlite", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.Path)
            && string.IsNullOrWhiteSpace(options.ConnectionString);
        if (!needsFile) return new ScratchDatabase(null);
        var folder = Directory.CreateTempSubdirectory("ormlite-demo-").FullName;
        options.Path = Path.Combine(folder, "demo.db");
        return new ScratchDatabase(folder);
    }

    // Deletes the scratch folder. Returns true when nothing is left (including when there never was a folder).
    public bool Remove()
    {
        if (folder is null) return true;
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        return !Directory.Exists(folder);
    }

    public void Dispose() => Remove();
}
