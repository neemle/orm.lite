using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.LibraryBaseRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class LibraryBaseGeneratorTests
{
    private const string Library = """
        namespace Lib
        {
            public abstract class LibBase
            {
                [Neemle.Orm.Lite.Key] public System.Guid Id { get; set; }
                public string Visible { get; set; } = "";
                public string Guarded { get; protected internal set; } = "";
                public string Hidden { get; internal set; } = "";
            }
        }
        """;

    private const string PrivateKeyLibrary = """
        namespace Lib
        {
            public abstract class LibBase
            {
                [Neemle.Orm.Lite.Key] public System.Guid Id { get; private set; }
                public string Visible { get; set; } = "";
            }
        }
        """;

    private const string UnmappedLibrary = """
        namespace Lib
        {
            public abstract class LibBase
            {
                [Neemle.Orm.Lite.Key] public System.Guid Id { get; set; }
                public byte[] RowVersion { get; set; } = System.Array.Empty<byte>();
            }
        }
        """;

    private const string WithAccess =
        "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"" + UserAssembly + "\")]\n" + Library;

    private const string Child = """
        [Table("children")]
        public class Child : Lib.LibBase
        {
            public string Extra { get; set; } = "";
        }
        """;

    [Fact(
        DisplayName = "B21 a protected internal setter of a base class in another assembly is refused once, on "
        + "the entity type, with zero CS errors")]
    public void A_protected_internal_setter_of_another_assembly_is_refused()
    {
        var (diagnostics, output) = Run(Library, Child);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(PropertyId, refusal.Id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Child.Guarded'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("Child", SpanText(refusal));
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "B21 the same setter is a column when the other assembly gives access to internals")]
    public void A_protected_internal_setter_with_granted_access_is_mapped()
    {
        var (diagnostics, output) = Run(WithAccess, Child);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        Assert.Contains("e.Guarded = ", TextOf(output), StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "B21 KNOWN LIMIT an internal setter of a base class in another assembly is not imported by "
        + "the compiler, so the property is skipped without a diagnostic")]
    public void An_internal_setter_of_another_assembly_is_skipped_silently()
    {
        var (diagnostics, output) = Run(Library, Child);

        Assert.DoesNotContain(diagnostics, d => d.GetMessage().Contains("Hidden", StringComparison.Ordinal));
        Assert.Empty(CompileErrors(output));
        Assert.DoesNotContain("Hidden", TextOf(output), StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "B21 a key with a private setter in a base class of another assembly gets one ORMLITE023 on "
        + "the entity type and no keyed API is generated")]
    public void A_private_set_key_of_another_assembly_is_reported_once()
    {
        // Roslyn imports a setter that is not accessible from the other assembly as no setter at all. Before ORML-60
        // that key was lost without a word (the B21 limit); a [Key] the generator cannot map is now reported.
        var (diagnostics, output) = Run(PrivateKeyLibrary, Child);

        var report = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE023", report.Id);
        Assert.Contains("'Child.Id'", report.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("no set accessor", report.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("another assembly", report.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("Child", SpanText(report));
        Assert.Empty(CompileErrors(output));
        Assert.DoesNotContain("GetAsync", TextOf(output), StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "B21 an unmapped property of a base class in another assembly gives one ORMLITE001, placed on "
        + "the entity type")]
    public void An_unmapped_inherited_type_of_another_assembly_is_reported_on_the_entity()
    {
        var (diagnostics, output) = Run(UnmappedLibrary, Child);

        var report = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE001", report.Id);
        Assert.Contains("Child.RowVersion", report.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("Child", SpanText(report));
        Assert.Empty(CompileErrors(output));
    }
}
