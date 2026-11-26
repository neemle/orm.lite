using System.Reflection;
using System.Runtime.CompilerServices;

namespace Neemle.OrmLite.Tests.Corpus;

public sealed record SurfaceMember(string Type, string Key);

public static class CorpusSurface
{
    private static readonly (Type Type, bool Internal)[] Types =
    [
        (typeof(Query<>), false),
        (typeof(Row<>), false),
        (typeof(UpsertSet<>), false),
        (typeof(UpsertSet), false),
        (typeof(Db), false),
        (typeof(LikePattern), false),
        (typeof(MigrationBuilder), false),
        (typeof(TableBuilder), false),
        (typeof(Migration), false),
        (typeof(ForeignKeyActionDdl), false),
        (typeof(LiveOnlyConversionDdl), false),
        (typeof(SoftDeleteColumnDdl), false),
        (typeof(RowHistoryDdl), true),
        (typeof(SoftDeleteSql), false),
        (typeof(RowHistoryRead), false),
        (typeof(RowHistoryRevert), false),
        (typeof(SoftRestore), false),
        (typeof(SoftTrashRead), false),
        (typeof(ConstraintColumns), false),
        (typeof(AdvisorySessionLocks), false),
        (typeof(UnitOfWork), false),
        (typeof(OrmContext), false),
        (typeof(DbActor), false),
        (typeof(DbLane), false),
        (typeof(OrmLiteSettings), false),
        (typeof(GatedDataSource), false),
        (typeof(OrmEnum), false),
        (typeof(DbExtensions), false),
        (typeof(InsertManyChunks), true),
        (typeof(ConflictGuard), true),
    ];

    public static readonly IReadOnlyDictionary<string, string> Exclusions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Query.StreamAsync/1"] = "cold row-stream capture shares CompileSelect text; SdetSoftReadCensusTests executes visibility and SdetProviderTests executes parity, no-write and Completion on both providers",
        ["Query.StreamProjectAsync/3"] = "cold projection-stream capture shares CompileProject text; SdetProviderTests executes frozen columns, no-write and Completion on both providers",
        ["Query.StreamProjectionAsync/2"] = "cold configured-projection capture shares CompileProjection text; SdetProviderTests executes no-write and Completion on both providers",
        ["Query.StreamColumnAsync/2"] = "row-wise scalar stream shares CompileProject text, not ScalarAsync first-row cap; SdetProviderTests executes typed NULL/default, no-write and Completion on both providers",
        ["Query.get_StreamMap/0"] = "internal captured trusted mapper identity; SdetPrimaryCleanupTests and SdetProviderTests exercise exact cause and detached-row mapping, emits no SQL",
        ["Query.get_StreamBinding/0"] = "internal provider/data-source snapshot; producer gated-open and independent real-provider diagnostic/lock witnesses execute this path, emits no SQL",
        ["Query.get_StreamBorrowed/0"] = "internal borrowed connection/transaction snapshot; SdetBorrowedBoundaryTests executes exact-identity isolation and mismatch refusal, emits no SQL",
        ["Query.get_StreamLocked/0"] = "internal row-lock metadata snapshot; SdetBorrowedBoundaryTests and SdetProviderTests execute transaction prerequisite and actual PostgreSQL lock path, emits no SQL",
        ["Query.BuildSelect/0"] = "internal tree builder beneath CompileSelect; the corpus pins the text it compiles to and QueryTreeTests pin the tree",
        ["Query.BuildProject/1"] = "internal tree builder beneath CompileProject; the corpus pins the text it compiles to and QueryTreeTests pin the tree",
        ["Query.BuildProjection/0"] = "internal tree builder beneath CompileProjection; the corpus pins the text it compiles to and QueryTreeTests pin the tree",
        ["Query.BuildAggregates/0"] = "internal tree builder beneath CompileAggregates; the corpus pins the text it compiles to and QueryTreeTests pin the tree",
        ["Query.BuildScalar/1"] =
            "internal tree builder beneath CompileScalar; the clause-free text is pinned by the executed "
            + "query-scalar-soft and query-scalar-null-answer-is-the-default entries and the ordered text by "
            + "ScalarRenderingTests",
        ["Query.CompileScalar/1"] =
            "the statement ScalarAsync runs; the clause-free text is pinned by the executed "
            + "query-scalar-soft and query-scalar-null-answer-is-the-default entries, which run through it, "
            + "and the order and offset text on both providers by ScalarRenderingTests",
        ["Query.RenderScalar/1"] =
            "text of CompileScalar; pinned by the same executed entries and by ScalarRenderingTests on both providers",
        ["Query.BuildView/2"] = "internal tree builder beneath AsView; the corpus pins the CREATE VIEW text it compiles to in ddl-builder/create-view-* and view-over-a-join-and-an-aggregate",
        ["Query.Definition/1"] = "internal factory of the provider-bound query that MigrationBuilder.From returns; its use is pinned by every ddl-builder view entry",
        ["Query.get_SourceTable/0"] = "internal accessor read only by ViewColumns; the table it names is pinned by ddl-builder/create-view-over-a-filtered-query",
        ["Query.get_State/0"] = "internal accessor read only by ViewColumns; the scope it carries is pinned by ddl-builder/view-over-a-join-and-an-aggregate",
        ["Query.get_Translator/0"] = "internal accessor read only by ViewColumns; the predicates it translates are pinned by ddl-builder/view-over-a-join-and-an-aggregate",
        ["Query.get_WhereTerms/0"] = "node-tree accessor beside WhereClauses; the terms it exposes are the ones WhereClauses renders, pinned by query-read/select-render-is-repeatable-and-exposes-the-where-terms",
        ["ConstraintColumns..ctor/2"] = "record constructor of the result type; the type emits no text, ConstraintColumns.ForAsync is pinned by the executed/constraint-columns-* transcripts",
        ["OrmContext.DisposeAsync/0"] = "releases the data sources an options-built context owns; it emits no text",
        ["OrmContext.MigrateAsync/1"] = "applies the contributions the generated assemblies registered through MigrationRunner.RunAsync, whose transcripts are the generator-*/migration-runner-* entries; MigrateAsyncTests apply them",
        ["OrmContext.MigrateAsync/2"] = "forwards explicit contributions to MigrationRunner.RunAsync, whose transcripts are the generator-*/migration-runner-* entries; MigrateAsyncTests apply them",
        ["InsertManyChunks.RunAsync/5"] = "internal runner reached only through Query.InsertManyAsync; its transcripts are the executed/insert-many-* entries",
        ["ConflictGuard.RequireDefined/1"] = "internal guard reached through Query.RenderUpsert; the message is pinned by refusals/upsert-with-an-undefined-conflict-action",
        ["ConflictGuard.RequireTarget/2"] = "internal guard reached through Query.RenderUpsert; the message is pinned by refusals/upsert-update-without-a-target",
        ["MigrationBuilder.Add/1"] = "appends an already built operation node; the nodes it receives are the ones the typed methods build, pinned by Nodes/DdlOperationTests",
        ["MigrationBuilder.AddDecimalColumn/6"] = "typed decimal column; the corpus predates typed columns, text pinned by Nodes/DdlOperationTests",
        ["MigrationBuilder.DropForeignKey/2"] = "typed inverse of AddForeignKey added with the reversibility classifier, pinned by Nodes/MigrationReversibilityTests",
        ["TableBuilder.DecimalColumn/6"] = "typed decimal column; the corpus predates typed columns, text pinned by Nodes/DdlOperationTests",
        ["LikePattern.EscapeLiteral/1"] =
            "escape helper beneath every Contains, StartsWith and EndsWith predicate; its text is pinned by LikePatternTests",
        ["LikePattern.Contains/1"] = "wraps EscapeLiteral in the percent signs of a LIKE pattern, pinned by LikePatternTests",
        ["LikePattern.StartsWith/1"] = "appends the percent sign to EscapeLiteral, pinned by LikePatternTests",
        ["LikePattern.EndsWith/1"] = "prepends the percent sign to EscapeLiteral, pinned by LikePatternTests",
        ["DbExtensions.AddParam/3"] =
            "converts a value to the ADO.NET parameter it binds and emits no statement; "
            + "pinned by DbExtensionsAddParamTests",
        ["UpsertSet.ToParam/2"] =
            "data constructor of an upsert term; the term renders no text, "
            + "the upsert text is pinned by the query-upsert entries",
        ["UpsertSet.ToJsonDocumentParam/2"] =
            "data constructor of an upsert term for a JSON document; the term renders no text, "
            + "the upsert text is pinned by the query-upsert entries",
        ["RowHistoryDdl.Preamble/1"] = "public form of PreambleFor under the library default actor setting, for the provider it is given; its text is pinned by row-history/ddl-preamble-under-the-default-actor-setting",
        ["RowHistoryDdl.SetHash/2"] = "the form of SetHash that names the actor setting; under the library default it equals SetHash, which the hash/set-hash-vectors entry pins, and a custom setting is pinned by Ports.Tests/MigrationIdentityTests",
        ["RowHistoryDdl.Install/1"] ="node factory beneath Reconcile; the statements it lowers to are pinned by the row-history/ddl-reconcile-* entries",
    };

    public static IReadOnlyList<Type> SurveyedTypes => Types.Select(t => t.Type).ToList();

    public static IReadOnlyList<Type> InternalTypes => Types.Where(t => t.Internal).Select(t => t.Type).ToList();

    public static IReadOnlyList<SurfaceMember> Members()
    {
        var members = new List<SurfaceMember>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var (type, includeInternal) in Types)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var bases = new List<MethodBase>(type.GetMethods(flags));
            bases.AddRange(type.GetConstructors(flags));
            foreach (var method in bases)
            {
                if (method.IsPrivate || method.IsFamily || method.IsFamilyAndAssembly) continue;
                if (method.IsAssembly && !includeInternal && !(type == typeof(Query<>))) continue;
                if (method.GetCustomAttribute<CompilerGeneratedAttribute>() is not null) continue;
                if (method.IsSpecialName && !method.IsConstructor && !IsWantedAccessor(type, method)) continue;
                if (method.Name is "Equals" or "GetHashCode" or "ToString" or "Deconstruct" or "PrintMembers" or "<Clone>$") continue;
                if (method.IsStatic && method.IsConstructor) continue;
                if (method.IsConstructor && !method.IsPublic) continue;
                var key = IlReferences.KeyOf(method);
                if (seen.Add(key)) members.Add(new SurfaceMember(TypeName(type), key));
            }
        }
        return members;
    }

    private static bool IsWantedAccessor(Type type, MethodBase method)
    {
        if (!method.Name.StartsWith("get_", StringComparison.Ordinal)) return false;
        if (type == typeof(Query<>)) return true;
        return method.IsStatic && method.IsPublic;
    }

    public static string TypeName(Type type) =>
        type.Name.Contains('`') ? type.Name[..type.Name.IndexOf('`')] : type.Name;
}
