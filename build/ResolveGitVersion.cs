using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

public sealed class ResolveGitVersion : Microsoft.Build.Utilities.Task
{
    private const string InitialVersion = "0.1.0";
    private const string Number = "(?:0|[1-9][0-9]*)";
    private const string Identifier = "(?:" + Number + "|[0-9]*[a-zA-Z-][a-zA-Z0-9-]*)";
    private static readonly Regex SemVer = new Regex(
        "\\Av?(?<core>" + Number + "\\." + Number + "\\." + Number + ")"
        + "(?:-" + Identifier + "(?:\\." + Identifier + ")*)?\\z", RegexOptions.CultureInvariant);

    [Required] public string Root { get; set; } = "";
    public bool RequireReleaseTag { get; set; }
    public string ExpectedTag { get; set; } = "";
    [Output] public string ResolvedVersion { get; private set; } = "";

    public override bool Execute()
    {
        try
        {
            ResolvedVersion = Resolve();
            return true;
        }
        catch (Exception error)
        {
            Log.LogError("ORM.lite version: " + error.Message);
            return false;
        }
    }

    private string Resolve()
    {
        var gitDirectory = Path.Combine(Root, ".git");
        if (!Directory.Exists(gitDirectory) && !File.Exists(gitDirectory))
        {
            if (RequireReleaseTag || ExpectedTag.Length != 0)
                throw new InvalidOperationException("A release requires a Git checkout and a SemVer tag on HEAD.");
            return InitialVersion + "-dev.unknown";
        }

        var taggedVersions = Tags(Git("tag --points-at HEAD")).ToArray();
        var versions = taggedVersions.Select(t => t.TrimStart('v')).Distinct(StringComparer.Ordinal).ToArray();
        if (versions.Length > 1)
            throw new InvalidOperationException("HEAD has multiple SemVer versions: " + string.Join(", ", versions) + ".");
        if (ExpectedTag.Length != 0 && !taggedVersions.Contains(ExpectedTag, StringComparer.Ordinal))
            throw new InvalidOperationException("The release tag '" + ExpectedTag + "' must be a supported SemVer tag on HEAD.");
        if (versions.Length == 1)
            return versions[0];
        if (RequireReleaseTag)
            throw new InvalidOperationException("A release requires a SemVer tag on HEAD, such as v1.2.3 or v1.2.3-rc.1.");

        var core = Tags(Git("tag --merged HEAD"))
            .Select(t => Version.Parse(SemVer.Match(t).Groups["core"].Value))
            .OrderByDescending(version => version)
            .FirstOrDefault();
        return (core?.ToString() ?? InitialVersion) + "-dev.g" + Git("rev-parse --short=7 HEAD");
    }

    private static IEnumerable<string> Tags(string output)
    {
        foreach (var tag in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = SemVer.Match(tag);
            if (match.Success && Version.TryParse(match.Groups["core"].Value, out var core)
                && core.Major < ushort.MaxValue && core.Minor < ushort.MaxValue && core.Build < ushort.MaxValue)
                yield return tag;
        }
    }

    private string Git(string arguments)
    {
        var start = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using (var process = Process.Start(start))
        {
            if (process == null)
                throw new InvalidOperationException("Could not start Git.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("git " + arguments + " failed: " + error.GetAwaiter().GetResult().Trim());
            return output.GetAwaiter().GetResult().Trim();
        }
    }
}
