#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
using System.Diagnostics;

var source = Directory.GetCurrentDirectory();
var fixture = Directory.CreateTempSubdirectory("ormlite-git-version-");
try
{
    var folder = fixture.FullName;
    File.Copy(Path.Combine(source, "Directory.Build.targets"), Path.Combine(folder, "Directory.Build.targets"));
    Directory.CreateDirectory(Path.Combine(folder, "build"));
    File.Copy(Path.Combine(source, "build", "ResolveGitVersion.cs"), Path.Combine(folder, "build", "ResolveGitVersion.cs"));
    File.WriteAllText(Path.Combine(folder, "Probe.csproj"),
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

    Expect("source archive", "0.1.0-dev.unknown");
    Refuse("release archive", "A release requires a Git checkout", "-p:OrmLiteRequireReleaseTag=true");

    Git("init", "-q");
    Git("config", "user.name", "ORM.lite test");
    Git("config", "user.email", "test@example.invalid");
    Git("add", ".");
    Git("commit", "-qm", "Initial version");
    Expect("untagged repository", "0.1.0-dev.g" + Git("rev-parse", "--short=7", "HEAD"));
    Refuse("untagged release", "A release requires a SemVer tag on HEAD", "-p:OrmLiteRequireReleaseTag=true");

    Git("tag", "v1.2.3");
    Expect("release ref name matches stable version", "1.2.3", "-p:OrmLiteExpectedTag=v1.2.3");
    Refuse("mismatched release tag", "must be a supported SemVer tag on HEAD", "-p:OrmLiteExpectedTag=v1.2.4");
    Git("tag", "v1.2.4");
    Refuse("ambiguous release", "HEAD has multiple SemVer versions");
    Git("tag", "-d", "v1.2.4");

    Git("commit", "--allow-empty", "-qm", "Development build");
    Git("tag", "v2.0.0-01");
    Expect("untagged commit after release", "1.2.3-dev.g" + Git("rev-parse", "--short=7", "HEAD"));
    Git("tag", "-a", "2.0.0-rc.1", "-m", "Release candidate");
    Expect("annotated prerelease", "2.0.0-rc.1", "-p:OrmLiteRequireReleaseTag=true");

    return 0;
}
finally
{
    fixture.Delete(recursive: true);
}

string Git(params string[] args)
{
    var result = Run("git", args);
    return result.Code == 0 ? result.Output : throw new InvalidOperationException(result.Output);
}

void Expect(string name, string expected, params string[] flags)
{
    var result = Resolve(flags);
    if (result.Code != 0 || result.Output != expected)
        throw new InvalidOperationException(name + " expected " + expected + ", got " + result.Output);
    Console.WriteLine("PASS: " + name + " → " + expected);
}

void Refuse(string name, string reason, params string[] flags)
{
    var result = Resolve(flags);
    if (result.Code == 0 || !result.Output.Contains(reason, StringComparison.Ordinal))
        throw new InvalidOperationException(name + ": " + result.Output);
    Console.WriteLine("PASS: " + name + " refused.");
}

(int Code, string Output) Resolve(string[] flags) =>
    Run("dotnet", ["msbuild", "Probe.csproj", "-nologo", "-t:ResolveOrmLiteVersion", "-getProperty:Version", .. flags]);

(int Code, string Output) Run(string file, string[] arguments)
{
    var start = new ProcessStartInfo(file)
    {
        WorkingDirectory = fixture.FullName,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    start.Environment.Remove("OrmLiteExpectedTag");
    start.Environment.Remove("OrmLiteRequireReleaseTag");
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var errors = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    return (process.ExitCode, (output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult()).Trim());
}
