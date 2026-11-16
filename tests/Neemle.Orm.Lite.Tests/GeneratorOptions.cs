using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Neemle.Orm.Lite.Tests;

internal sealed class GeneratorOptions : AnalyzerConfigOptionsProvider
{
    public const string PinnedTenant = "ScopeId";

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }

    private GeneratorOptions(Dictionary<string, string> values) => GlobalOptions = new Options(values);

    public override AnalyzerConfigOptions GlobalOptions { get; }

    public static GeneratorOptions Pinned() => Tenant(PinnedTenant);

    public static GeneratorOptions Unset() => new([]);

    public static GeneratorOptions Tenant(string value) =>
        new(new Dictionary<string, string> { ["build_property.OrmLiteDefaultTenant"] = value });

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
}
