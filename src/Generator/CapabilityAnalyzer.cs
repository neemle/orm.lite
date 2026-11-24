using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Neemle.OrmLite;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CapabilityAnalyzer : DiagnosticAnalyzer
{
    internal const string AttributeName = "Neemle.OrmLite.RequiresCapabilityAttribute";

    private static readonly DiagnosticDescriptor MemberNeedsCapability = new DiagnosticDescriptor(
        id: "ORMLITE016",
        title: "A query member needs a capability that a provider of the project does not support",
        messageFormat:
            "'{0}' needs the {1} capability; provider '{2}' does not support it. "
            + "Use another member or take '{2}' out of OrmLiteProviders.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MemberNeedsCapability);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var providers = CapabilityManifest.Read(
                start.Options.AnalyzerConfigOptionsProvider, start.Options.AdditionalFiles, start.CancellationToken);
            var checkable = providers.Providers
                .Select(providers.Find).Where(m => m is not null).Select(m => m!).ToImmutableArray();
            if (checkable.IsEmpty) return;
            start.RegisterOperationAction(operation => Check(operation, checkable), OperationKind.Invocation);
        });
    }

    private static void Check(OperationAnalysisContext context, ImmutableArray<ProviderManifest> providers)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod.OriginalDefinition;
        foreach (var capability in NeedsOf(invocation, method))
            foreach (var provider in providers.Where(p => CapabilityManifest.Refuses(p, capability)))
                context.ReportDiagnostic(Diagnostic.Create(
                    MemberNeedsCapability, invocation.Syntax.GetLocation(),
                    method.ContainingType.Name + "." + method.Name, capability, provider.Provider));
    }

    private static IEnumerable<string> NeedsOf(IInvocationOperation invocation, IMethodSymbol method) =>
        method.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == AttributeName)
            .Select(CapabilityName)
            .OfType<string>()
            .Concat(BuilderCapabilities.Of(invocation))
            .Distinct(StringComparer.Ordinal);

    private static string? CapabilityName(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length != 1) return null;
        var argument = attribute.ConstructorArguments[0];
        return argument.Type?.GetMembers().OfType<IFieldSymbol>()
            .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, argument.Value))?.Name;
    }
}
