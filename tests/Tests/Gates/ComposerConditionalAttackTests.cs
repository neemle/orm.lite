using System;
using System.IO;
using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

// Admitted reviewer test, kept as a regression guard (provenance: ORML-143, §803 FULL, round 2, finding F3; fixed under
// ORML-147). Committed unchanged.
public sealed class ComposerConditionalAttackTests
{
    [Fact]
    public void F3_Guard_detects_escape_in_net10_compiled_branch()
    {
        var root = RuntimeSources.LibraryRoot();
        var generator = Path.Combine(root, "src", "Generator");

        Assert.Contains(
            "<TargetFramework>net10.0</TargetFramework>",
            File.ReadAllText(Path.Combine(generator, "Generator.csproj")),
            StringComparison.Ordinal);

        const string source = """
            using Neemle.CSharpComposer;

            internal static class ComposerConditionalProbe
            {
                internal static void Probe(string text)
                {
                    _ = Cs.Parse(text);
                }
            }
            """;

        // This is only a syntax-tree path; no file is created.
        var path = Path.Combine(generator, "ComposerConditionalProbe.cs");
        var guarded = "#if NET10_0\n" + source + "\n#endif\n";

        // Controls: the call is detectable, including inside an active branch.
        Assert.NotEmpty(ComposerEscapeScan.Find([(path, source)]));
        Assert.NotEmpty(ComposerEscapeScan.Find(
            [(path, "#define NET10_0\n" + guarded)]));

        // NET10_0 is supplied by the generator's SDK build, but the current
        // scanner omits it and silently excludes the forbidden call.
        Assert.NotEmpty(ComposerEscapeScan.Find([(path, guarded)]));
    }
}
