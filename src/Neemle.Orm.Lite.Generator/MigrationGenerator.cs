using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.Orm.Lite;

[Generator]
public sealed class MigrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {

        var migrations = context.SyntaxProvider
            .ForAttributeWithMetadataName("Neemle.Orm.Lite.MigrationAttribute",
                static (n, _) => n is ClassDeclarationSyntax,
                static (ctx, _) =>
                {
                    var s = (INamedTypeSymbol)ctx.TargetSymbol;
                    var version = ctx.Attributes[0].ConstructorArguments.Length > 0
                        ? ctx.Attributes[0].ConstructorArguments[0].Value as string ?? ""
                        : "";
                    var attribute = ctx.Attributes[0];
                    var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                        ?? s.Locations.FirstOrDefault() ?? Location.None;
                    var (expression, unnamed) = ReadReversibility(attribute);
                    return new Mig(version, s.ToDisplayString(), expression, unnamed, OverridesDown(s), location);
                })
            .Collect();

        var hasSoft = context.SyntaxProvider
            .ForAttributeWithMetadataName("Neemle.Orm.Lite.TableAttribute",
                static (n, _) => n is ClassDeclarationSyntax,
                static (ctx, _) => ctx.TargetSymbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Neemle.Orm.Lite.SoftDeleteAttribute"))
            .Collect()
            .Select(static (flags, _) => flags.Any(f => f));

        var asmToken = context.CompilationProvider.Select(static (c, _) => OrmGenerator.AsmToken(c));
        context.RegisterSourceOutput(migrations.Combine(asmToken).Combine(hasSoft),
            static (spc, pair) => Emit(spc, pair.Left.Left, pair.Left.Right, pair.Right));
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<Mig> migs, string asmToken, bool hasSoft)
    {
        foreach (var m in migs.Where(m => m.Reversibility is null))
            spc.ReportDiagnostic(Diagnostic.Create(UnnamedReversibility, m.Location, m.Type, m.Version, m.Unnamed));

        var irreversible = IrreversibleExpression;
        foreach (var m in migs.Where(m => m.Reversibility == irreversible && m.OverridesDown))
            spc.ReportDiagnostic(Diagnostic.Create(IrreversibleWithDown, m.Location, m.Type, m.Version));

        var ordered = migs.Where(m => m.Reversibility is not null).OrderBy(m => m.Version, StringComparer.Ordinal).ToArray();

        var model = new
        {
            Migrations = ordered.Select(m => new { m.Version, m.Type, m.Reversibility, Down = DownExpression(m, irreversible) }).ToList(),
            AsmToken = asmToken,
            HasSoft = hasSoft,
        };
        var code = global::Neemle.Orm.Lite.Templating.Template.RenderResource(
            typeof(MigrationGenerator).Assembly, "Migrations.cs-template", model);
        spc.AddSource("OrmLite.Migrations.g.cs", code);
    }

    private static readonly DiagnosticDescriptor UnnamedReversibility = new DiagnosticDescriptor(
        id: "ORMLITE006",
        title: "A [Migration] must declare a named Reversibility member",
        messageFormat: "[Migration(\"{1}\")] on '{0}' declares Reversibility value '{2}', which is not a named member of Neemle.Orm.Lite.Reversibility. Declare Reversibility.Reversible or Reversibility.Irreversible; default and (Reversibility)0 do not stand for a declaration, so this migration is not emitted.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IrreversibleWithDown = new DiagnosticDescriptor(
        id: "ORMLITE003",
        title: "An Irreversible [Migration] must not override Down",
        messageFormat: "[Migration(\"{1}\")] on '{0}' is Reversibility.Irreversible and overrides Down. An irreversible migration has no down; remove the Down override or declare Reversibility.Reversible. The entry is emitted without a Down.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string IrreversibleExpression = "global::Neemle.Orm.Lite.Reversibility.Irreversible";

    private static string DownExpression(Mig m, string irreversible) =>
        m.OverridesDown && m.Reversibility != irreversible
            ? "static b => new global::" + m.Type + "().Down(b)"
            : "null";

    private static bool OverridesDown(INamedTypeSymbol type)
    {
        for (var t = type; t is not null && t.ToDisplayString() != "Neemle.Orm.Lite.Migration"; t = t.BaseType)
            foreach (var member in t.GetMembers("Down"))
                if (member is IMethodSymbol { IsOverride: true, Parameters.Length: 1 } method
                    && method.Parameters[0].Type.ToDisplayString() == "Neemle.Orm.Lite.MigrationBuilder")
                    return true;
        return false;
    }

    private static (string? Expression, string Unnamed) ReadReversibility(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length < 2) return (null, "missing");
        var argument = attribute.ConstructorArguments[1];
        var member = argument.Type?.GetMembers().OfType<IFieldSymbol>()
            .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, argument.Value));
        return member is null
            ? (null, argument.Value?.ToString() ?? "null")
            : ("global::Neemle.Orm.Lite.Reversibility." + member.Name, "");
    }

    private sealed record Mig(string Version, string Type, string? Reversibility, string Unnamed, bool OverridesDown, Location Location);
}
