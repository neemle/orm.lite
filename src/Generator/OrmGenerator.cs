using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// The incremental generator of the entities. It wires the pipeline together: the EntityReader reads each table and
/// view type into an entity model (compared by value, so an edit that does not change a model does not regenerate),
/// the models are ordered and given their tenant, and the EntityEmitter reports and writes the one generated file.
/// This type also holds the entity model records that the reading, the reporting and the emitting share.
/// </summary>
[Generator]
public sealed class OrmGenerator : IIncrementalGenerator
{
    private const string DefaultTenantProperty = "build_property.OrmLiteDefaultTenant";

    /// <summary>Tracking names of the per-entity steps; tests read them to see whether an edit regenerated.</summary>
    public const string TablesStep = "OrmLiteTables";

    public const string ViewsStep = "OrmLiteViews";

    public const string JsonbClrTypeName = "Neemle.OrmLite.Jsonb";

    internal const string DefaultTenant = "TenantId";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tables = context.SyntaxProvider
            .ForAttributeWithMetadataName("Neemle.OrmLite.TableAttribute",
                static (n, _) => IsEntityDeclaration(n),
                static (ctx, _) => EntityReader.ReadEntity((INamedTypeSymbol)ctx.TargetSymbol, false))
            .WithTrackingName(TablesStep)
            .Collect();
        var views = context.SyntaxProvider
            .ForAttributeWithMetadataName("Neemle.OrmLite.ViewAttribute",
                static (n, _) => IsEntityDeclaration(n),
                static (ctx, _) => EntityReader.ReadEntity((INamedTypeSymbol)ctx.TargetSymbol, true))
            .WithTrackingName(ViewsStep)
            .Collect();
        var entities = tables.Combine(views)
            .Select(static (pair, _) => EntityOrder.Of(pair.Left.AddRange(pair.Right)));

        var defaultTenant = context.AnalyzerConfigOptionsProvider.Select(static (p, _) =>
            p.GlobalOptions.TryGetValue(DefaultTenantProperty, out var value) ? (value ?? "").Trim() : "");
        var resolved = entities.Combine(defaultTenant)
            .Select(static (pair, _) => DefaultTenants.Resolve(pair.Left, pair.Right));
        RegisterOutputs(context, resolved, defaultTenant);
    }

    internal static string AsmToken(Compilation c)
    {
        var name = c.AssemblyName ?? "App";
        var sb = new StringBuilder();
        foreach (var ch in name) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        var s = sb.Length == 0 ? "App" : sb.ToString();
        if (char.IsDigit(s[0])) s = "_" + s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    internal static bool Dropped(Entity e) => e.IsStatic || e.NameIssue is not null;

    internal static bool Refused(Entity e) => !e.IsView && e.Props.Count(p => p.IsKey) > 1;

    internal static Prop? FirstKey(Entity e) => e.Props.FirstOrDefault(p => p.IsKey);

    internal static bool KeyedApiWithheld(Entity e) => Refused(e) || !e.LostKeys.IsDefaultOrEmpty;

    private static void RegisterOutputs(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<Entity>> resolved,
        IncrementalValueProvider<string> defaultTenant)
    {
        var asmToken = context.CompilationProvider.Select(static (c, _) => AsmToken(c));
        context.RegisterSourceOutput(
            defaultTenant, static (spc, value) => SoftDeleteDiagnostics.ReportDefaultTenant(spc, value));
        var providers = context.AnalyzerConfigOptionsProvider
            .Combine(context.AdditionalTextsProvider.Collect())
            .Select(static (pair, ct) => CapabilityManifest.Read(pair.Left, pair.Right, ct));
        context.RegisterSourceOutput(providers, static (spc, set) => CapabilityDiagnostics.ReportProviders(spc, set));
        context.RegisterSourceOutput(
            resolved.Combine(asmToken).Combine(providers),
            static (spc, pair) => GeneratorGuard.Run(
                spc, nameof(OrmGenerator),
                () => EntityEmitter.EmitEntities(spc, pair.Left.Left, pair.Left.Right, pair.Right)));
    }

    private static bool IsEntityDeclaration(SyntaxNode node) =>
        node is ClassDeclarationSyntax
        || (node is RecordDeclarationSyntax
            && node.RawKind == (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.RecordDeclaration);

    internal sealed record SoftDeclaration(
        string TenantName, bool TenantDeclared, string DefaultName, bool Platform, Prop? TenantProp,
        EquatableArray<string> UnversionedColumns, EquatableArray<string> UnknownUnversioned, Location Location,
        string? LabelName, Prop? LabelProp, EquatableArray<string> OwnerColumns, EquatableArray<string> UnknownOwners,
        EquatableArray<string> PinnedColumns, EquatableArray<string> UnknownPinned,
        EquatableArray<string> StatusColumns, EquatableArray<string> UnknownStatus,
        EquatableArray<string> StatusPinnedOverlap);
    internal sealed record Entity(
        string Type, string Table, EquatableArray<Prop> Props, EquatableArray<IndexDef> Indexes,
        EquatableArray<Relation> Relations, EquatableArray<Collection> Collections,
        EquatableArray<ManyToMany> ManyToMany, bool Managed, EquatableArray<Unmapped> Unmapped,
        string? SoftDeleteColumn, SoftDeleteIssue? SoftDeleteIssue, SoftDeclaration? Soft, string Access,
        bool IsView = false, Location? Location = null, EquatableArray<MemberIssue> MemberIssues = default,
        string? TypeIssue = null, string? RequiredMissing = null, EquatableArray<string> LostKeys = default,
        EquatableArray<string> ExcludedMembers = default, bool IsStatic = false)
    {
        public EquatableArray<ForeignKeyIntent> ForeignKeyIntents { get; init; } = default;

        public EquatableArray<ForeignKeyModel> ForeignKeys { get; init; } = default;

        public EquatableArray<CalculatedProp> Calculated { get; init; } = default;

        public string Full { get; init; } = "";

        public string? NameIssue { get; init; }
    }
    internal sealed record CalculatedProp(Prop Property, string SourceProperty);
    internal sealed record Unmapped(string Property, string TypeName, Location Location, string Note = "");
    internal sealed record SoftDeleteIssue(string Property, string Reason, Location Location);
    internal sealed record Relation(string Nav, string TargetType, string FkProp, bool FkNullable)
    {
        public string TargetFull { get; init; } = "";

        public string? FkCs { get; init; }

        public bool FkValue { get; init; }
    }
    internal sealed record Collection(string Nav, string ChildType, string InverseFk, string ParentKey)
    {
        public string ChildFull { get; init; } = "";
    }
    internal sealed record ManyToMany(
        string Nav, string OtherType, string OtherFull, string OtherTable, string OtherKeyCol, string OtherKeyCs,
        string JoinTable, string ThisKey, string OtherKey, string? OtherSoftColumn);
    internal sealed record Prop(
        string Name, string Column, string Type, string Read, bool IsKey, bool Nullable, string Cs, int Precision = 0,
        int Scale = 0, bool KeyByName = false, bool InInit = false, bool NullableValue = false);
    internal sealed record MemberIssue(string Member, string Reason, Location Location);
    internal sealed record ChildGuard(
        HashSet<string> Dropped,
        HashSet<(string Type, string Member)> Excluded,
        HashSet<(string Entity, string Nav)> Blocked);
    internal sealed record EmitScope(HashSet<string> AllTables, HashSet<string> Refused, ChildGuard Guard);
    internal sealed record TermDef(string Column, bool Descending, int Nulls, string? Collation, bool Lower);
    internal sealed record CondDef(string Column, string Op, string Kind, string Value);
    internal sealed record IndexDef(
        string Name, EquatableArray<TermDef> Terms, bool Unique, bool LiveOnly, bool ExplicitName, Location? Location,
        EquatableArray<CondDef> Filter, EquatableArray<string> Problems)
    {
        internal ImmutableArray<string> Columns => [.. Terms.Select(t => t.Column)];

        internal bool IsPlain =>
            Filter.IsEmpty && Terms.All(t => !t.Descending && t.Nulls == 0 && t.Collation is null && !t.Lower);
    }
}
