using SourceLayout = Neemle.OrmLite.Generator.GeneratedSourceLayout.GeneratedSourceLayout;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Rendering;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Generator;

[Generator]
public sealed class MigrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {

        var migrations = context.SyntaxProvider
            .ForAttributeWithMetadataName("Neemle.OrmLite.MigrationAttribute",
                static (n, _) => MigrationShape.IsDeclaration(n),
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
                    return new Mig(
                        version, s.ToDisplayString(), expression, unnamed, OverridesDown(s), location,
                        MigrationScan.Of(s), MigrationShape.IssueOf(s));
                })
            .WithTrackingName(MigrationsStep)
            .Collect();

        var asmToken = context.CompilationProvider.Select(static (c, _) => OrmGenerator.AsmToken(c));
        context.RegisterSourceOutput(
            migrations.Combine(asmToken),
            static (spc, pair) => GeneratorGuard.Run(
                spc, nameof(MigrationGenerator), () => Emit(spc, pair.Left, pair.Right)));
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<Mig> migs, string asmToken)
    {
        foreach (var m in migs.Where(m => m.Reversibility is null))
            spc.ReportDiagnostic(Diagnostic.Create(UnnamedReversibility, m.Location, m.Type, m.Version, m.Unnamed));

        var irreversible = IrreversibleMember;
        foreach (var m in migs.Where(m => m.Reversibility == irreversible && m.OverridesDown))
            spc.ReportDiagnostic(Diagnostic.Create(IrreversibleWithDown, m.Location, m.Type, m.Version));

        foreach (var m in migs.Where(m => m.Reversibility is not null))
            ReportAudit(spc, m);
        MigrationShape.Report(spc, migs);

        var ordered = MigrationVersions.Check(spc, migs)
            .Where(m => m.Reversibility is not null && m.Shape is null)
            .OrderBy(m => m.Version, StringComparer.Ordinal)
            .ThenBy(m => m.Type, StringComparer.Ordinal)
            .ToArray();

        spc.AddSource(MigrationsHint, SourceLayout.Wrap(MigrationsSource(ordered, asmToken)));
        spc.AddSource(RegistrationHint, SourceLayout.Wrap(RegistrationSource(asmToken)));
    }

    public const string MigrationsStep = "OrmLiteMigrations";

    internal const string MigrationsHint = "OrmLite.Migrations.g.cs";

    internal const string RegistrationHint = "OrmLite.MigrationRegistry.g.cs";

    private const string IrreversibleMember = "Irreversible";

    private static string MigrationsSource(System.Collections.Generic.IReadOnlyList<Mig> ordered, string asmToken)
    {
        var contributions = ordered.Select(m => Syn.Static("MigrationContribution", "For")
            .OfTypes(Cs.Type("global::" + m.Type))
            .Of(Syn.Str(m.Version), Cs.Id("Reversibility").Dot(m.Reversibility!), Syn.Flag(m.OverridesDown))).ToArray();
        return EntityEmitter.NewFile()
            .Class(asmToken + "Migrations").Public().Static()
                .Field(Syn.Lite("MigrationContribution").Array(), "Contributions").Public().Static().ReadOnly()
                    .Initialize(Cs.Id(asmToken + "Schema").Dot("Instance").Dot("Contributions").Of(contributions)).End()
            .End()
            .Render(new RenderOptions());
    }

    private static string RegistrationSource(string asmToken)
    {
        var register = Syn.Static("OrmMigrationRegistry", "Register").Of(
            Syn.Str(asmToken), Cs.Lambda(Cs.Id(asmToken + "Migrations").Dot("Contributions")));
        return EntityEmitter.NewFile()
            .Class("OrmMigrationsInit").Internal().Static()
                .Method(Cs.Void, "Init").Internal().Static()
                    .Attribute(Cs.Attribute(Syn.Sys("Runtime.CompilerServices.ModuleInitializer")))
                    .Body(Cs.Statement(register)).End()
            .End()
            .Render(new RenderOptions());
    }

    private static readonly DiagnosticDescriptor ReversibilityDisagrees = new DiagnosticDescriptor(
        id: "ORMLITE017",
        title: "A [Migration] Reversibility disagrees with the operations of its Up",
        messageFormat: "[Migration(\"{1}\")] on '{0}' {2}",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static void ReportAudit(SourceProductionContext spc, Mig m)
    {
        var scan = m.Scan;
        if (scan.Opaque) return;
        var invertible = scan.Lossy.Length == 0 && scan.Unknown.Length == 0;
        if (m.Reversibility == "Reversible" && !m.OverridesDown && !invertible)
            spc.ReportDiagnostic(Diagnostic.Create(
                ReversibilityDisagrees, m.Location, m.Type, m.Version,
                "declares Reversibility.Reversible and does not override Down, but its Up holds operations with "
                + "no automatic inverse: "
                + string.Join(", ", scan.Lossy.Concat(scan.Unknown).Distinct(StringComparer.Ordinal))
                + ". Override Down or declare Reversibility.Irreversible."));
        if (m.Reversibility == IrreversibleMember && invertible && scan.ReversibleCount > 0)
            spc.ReportDiagnostic(Diagnostic.Create(
                ReversibilityDisagrees, m.Location, m.Type, m.Version,
                "declares Reversibility.Irreversible although every operation of its Up has an automatic inverse. "
                + "Declare Reversibility.Reversible so that Down is derived."));
    }

    private static readonly DiagnosticDescriptor UnnamedReversibility = new DiagnosticDescriptor(
        id: "ORMLITE006",
        title: "A [Migration] must declare a named Reversibility member",
        messageFormat:
            "[Migration(\"{1}\")] on '{0}' declares Reversibility value '{2}', which is not a named member of "
            + "Neemle.OrmLite.Reversibility. Declare Reversibility.Reversible or Reversibility.Irreversible; "
            + "default and (Reversibility)0 do not stand for a declaration, so this migration is not emitted.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IrreversibleWithDown = new DiagnosticDescriptor(
        id: "ORMLITE003",
        title: "An Irreversible [Migration] must not override Down",
        messageFormat:
            "[Migration(\"{1}\")] on '{0}' is Reversibility.Irreversible and overrides Down. "
            + "An irreversible migration has no down; remove the Down override or declare Reversibility.Reversible. "
            + "The entry is emitted without a Down.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static bool OverridesDown(INamedTypeSymbol type)
    {
        for (var t = type; t is not null && t.ToDisplayString() != "Neemle.OrmLite.Migration"; t = t.BaseType)
            foreach (var member in t.GetMembers("Down"))
                if (member is IMethodSymbol { IsOverride: true, Parameters.Length: 1 } method
                    && method.Parameters[0].Type.ToDisplayString() == "Neemle.OrmLite.MigrationBuilder")
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
            : (member.Name, "");
    }

    internal sealed record Mig(
        string Version, string Type, string? Reversibility, string Unnamed, bool OverridesDown, Location Location,
        MigrationScan Scan, string? Shape = null);
}
