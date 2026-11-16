using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.Orm.Lite;

[Generator]
public sealed class OrmGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entities = context.SyntaxProvider
            .ForAttributeWithMetadataName("Neemle.Orm.Lite.TableAttribute",
                static (n, _) => n is ClassDeclarationSyntax,
                static (ctx, _) => ReadEntity((INamedTypeSymbol)ctx.TargetSymbol))
            .Collect();

        var repos = context.SyntaxProvider
            .ForAttributeWithMetadataName("Neemle.Orm.Lite.RepositoryAttribute",
                static (n, _) => n is ClassDeclarationSyntax,
                static (ctx, _) => ReadRepo((INamedTypeSymbol)ctx.TargetSymbol))
            .Collect();

        var defaultTenant = context.AnalyzerConfigOptionsProvider.Select(static (p, _) =>
            p.GlobalOptions.TryGetValue(DefaultTenantProperty, out var value) ? (value ?? "").Trim() : "");
        var resolved = entities.Combine(defaultTenant).Select(static (pair, _) => ResolveTenants(pair.Left, pair.Right));

        var asmToken = context.CompilationProvider.Select(static (c, _) => AsmToken(c));
        context.RegisterSourceOutput(defaultTenant, static (spc, value) => ReportDefaultTenant(spc, value));
        context.RegisterSourceOutput(resolved.Combine(asmToken), static (spc, pair) => EmitEntities(spc, pair.Left, pair.Right));
        context.RegisterSourceOutput(repos.Combine(resolved), static (spc, pair) => EmitRepos(spc, pair.Left, pair.Right));
    }

    private const string DefaultTenantProperty = "build_property.OrmLiteDefaultTenant";

    internal const string DefaultTenant = "TenantId";

    internal static string AsmToken(Compilation c)
    {
        var name = c.AssemblyName ?? "App";
        var sb = new StringBuilder();
        foreach (var ch in name) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        var s = sb.Length == 0 ? "App" : sb.ToString();
        if (char.IsDigit(s[0])) s = "_" + s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    private static Entity ReadEntity(INamedTypeSymbol s)
    {
        var table = AttrArg(s, "Neemle.Orm.Lite.TableAttribute") ?? Snake(s.Name);
        var props = new List<Prop>();
        var unmapped = new List<Unmapped>();
        var indexes = new List<IndexDef>();
        var navs = new List<(string Nav, string Target, string Fk)>();
        var collections = new List<(string Nav, string Child, string InvFk)>();
        var manyToMany = new List<ManyToMany>();
        foreach (var m in s.GetMembers().OfType<IPropertySymbol>())
        {
            if (m.IsStatic || m.GetMethod is null || m.SetMethod is null) continue;
            if (m.DeclaredAccessibility != Accessibility.Public) continue;
            if (m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Neemle.Orm.Lite.NotAColumnAttribute")) continue;
            var refAttr = m.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Neemle.Orm.Lite.ReferenceAttribute");
            if (m.Type is INamedTypeSymbol coll && coll.IsGenericType && (coll.Name == "List" || coll.Name == "IReadOnlyList" || coll.Name == "IList")
                && coll.TypeArguments.Length == 1 && coll.TypeArguments[0] is INamedTypeSymbol childT && HasAttr(childT, "Neemle.Orm.Lite.TableAttribute"))
            {
                var m2m = m.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Neemle.Orm.Lite.ManyToManyAttribute");
                if (m2m is { ConstructorArguments.Length: >= 3 })
                {
                    var (oTable, oKeyCol, oKeyCs, oFull) = EntityMeta(childT);
                    manyToMany.Add(new ManyToMany(m.Name, childT.Name, oFull, oTable, oKeyCol, oKeyCs,
                        m2m.ConstructorArguments[0].Value as string ?? "",
                        m2m.ConstructorArguments[1].Value as string ?? "",
                        m2m.ConstructorArguments[2].Value as string ?? "",
                        SoftColumnOf(childT)));
                    continue;
                }
                var invFk = (refAttr is { ConstructorArguments.Length: > 0 } ? refAttr.ConstructorArguments[0].Value as string : null) ?? (s.Name + "Id");
                collections.Add((m.Name, childT.Name, invFk));
                continue;
            }
            if (m.Type is INamedTypeSymbol target && (refAttr is not null || HasAttr(target, "Neemle.Orm.Lite.TableAttribute")))
            {
                var fkName = (refAttr is { ConstructorArguments.Length: > 0 } ? refAttr.ConstructorArguments[0].Value as string : null) ?? (m.Name + "Id");
                navs.Add((m.Name, target.Name, fkName));
                continue;
            }
            if (IsUnresolved(m.Type)) continue;
            var classified = Classify(m.Type);
            if (classified is null)
            {
                unmapped.Add(new Unmapped(m.Name, m.Type.ToDisplayString(),
                    m.Locations.FirstOrDefault() ?? Location.None));
                continue;
            }
            var (read, sql, cs) = classified.Value;
            var nullable = m.NullableAnnotation == NullableAnnotation.Annotated ||
                           (m.Type is INamedTypeSymbol nt && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T);
            var isKey = HasAttr(m, "Neemle.Orm.Lite.KeyAttribute") || m.Name == "Id";
            var column = ColumnOf(m);
            props.Add(new Prop(m.Name, column, sql, read, isKey, nullable, cs));
            var indexed = m.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Neemle.Orm.Lite.IndexedAttribute");
            if (indexed is not null)
                indexes.Add(new IndexDef($"ix_{table}_{column}", ImmutableArray.Create(column), NamedBool(indexed, "Unique")));
        }
        var colByName = props.ToDictionary(p => p.Name, p => p.Column);
        foreach (var a in s.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "Neemle.Orm.Lite.IndexAttribute"))
        {
            if (a.ConstructorArguments.Length == 0) continue;
            var cols = a.ConstructorArguments[0].Values
                .Select(v => v.Value as string ?? "")
                .Where(n => n.Length > 0)
                .Select(n => colByName.TryGetValue(n, out var c) ? c : Snake(n))
                .ToImmutableArray();
            if (cols.Length == 0) continue;
            var explicitName = a.NamedArguments.FirstOrDefault(n => n.Key == "Name").Value.Value as string;
            var liveOnly = NamedBool(a, "LiveOnly");
            var name = explicitName ?? $"ix_{table}_{string.Join("_", cols)}{(liveOnly ? "_live" : "")}";
            indexes.Add(new IndexDef(name, cols, NamedBool(a, "Unique"), liveOnly, explicitName is not null,
                a.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? s.Locations.FirstOrDefault() ?? Location.None));
        }
        var relations = new List<Relation>();
        foreach (var n in navs)
        {
            var fk = props.FirstOrDefault(p => p.Name == n.Fk);
            if (fk is not null) relations.Add(new Relation(n.Nav, n.Target, fk.Name, fk.Nullable));
        }
        var keyProp = props.FirstOrDefault(p => p.IsKey)?.Name ?? "Id";
        var colls = collections.Select(c => new Collection(c.Nav, c.Child, c.InvFk, keyProp)).ToImmutableArray();
        var managed = NamedBoolOrDefault(s, "Neemle.Orm.Lite.TableAttribute", "Managed", true);
        var (softDeleteColumn, softDeleteIssue) = ReadSoftDelete(s, props);
        return new Entity(s.ContainingNamespace.ToDisplayString(), s.Name, table, props.ToImmutableArray(), indexes.ToImmutableArray(), relations.ToImmutableArray(), colls, manyToMany.ToImmutableArray(), managed, unmapped.ToImmutableArray(), softDeleteColumn, softDeleteIssue, ReadSoftDeclaration(s, props), s.DeclaredAccessibility == Accessibility.Public ? "public" : "internal");
    }

    private const string PlatformTenant = "<platform>";

    private static SoftDeclaration? ReadSoftDeclaration(INamedTypeSymbol s, List<Prop> props)
    {
        if (SoftDeleteDeclaration(s) is not (var a, _, _)) return null;
        var location = a.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? s.Locations.FirstOrDefault() ?? Location.None;
        var declaredTenant = a.NamedArguments.FirstOrDefault(n => n.Key == "Tenant").Value.Value as string;
        var tenant = declaredTenant ?? "";
        var platform = tenant == PlatformTenant;
        var tenantProp = platform || declaredTenant is null ? null : props.FirstOrDefault(p => p.Name == tenant);
        var columns = new List<string>();
        var unknown = new List<string>();
        var declared = a.NamedArguments.FirstOrDefault(n => n.Key == "Unversioned").Value;
        if (declared.Kind == TypedConstantKind.Array)
            foreach (var v in declared.Values)
            {
                var name = v.Value as string ?? "";
                var prop = props.FirstOrDefault(p => p.Name == name);
                if (prop is null) unknown.Add(name);
                else columns.Add(prop.Column);
            }
        var labelName = a.NamedArguments.FirstOrDefault(n => n.Key == "Label").Value.Value as string;
        var labelProp = labelName is null ? null : props.FirstOrDefault(p => p.Name == labelName);
        var ownerColumns = new List<string>();
        var unknownOwners = new List<string>();
        var declaredOwners = a.NamedArguments.FirstOrDefault(n => n.Key == "Owners").Value;
        if (declaredOwners.Kind == TypedConstantKind.Array)
            foreach (var v in declaredOwners.Values)
            {
                var name = v.Value as string ?? "";
                var prop = props.FirstOrDefault(p => p.Name == name);
                if (prop is null) unknownOwners.Add(name);
                else if (!ownerColumns.Contains(prop.Column)) ownerColumns.Add(prop.Column);
            }
        var (pinnedColumns, unknownPinned) = ReadColumnList(a, "Pinned", props);
        var (statusColumns, unknownStatus) = ReadColumnList(a, "StatusColumns", props);
        var overlap = statusColumns.Where(c => pinnedColumns.Contains(c)).ToImmutableArray();
        return new SoftDeclaration(tenant, declaredTenant is not null, DefaultTenant, platform, tenantProp, columns.ToImmutableArray(), unknown.ToImmutableArray(), location,
            labelName, labelProp, ownerColumns.ToImmutableArray(), unknownOwners.ToImmutableArray(),
            pinnedColumns, unknownPinned, statusColumns, unknownStatus, overlap);
    }

    private static (ImmutableArray<string> Columns, ImmutableArray<string> Unknown) ReadColumnList(AttributeData a, string argument, List<Prop> props)
    {
        var columns = new List<string>();
        var unknown = new List<string>();
        var declared = a.NamedArguments.FirstOrDefault(n => n.Key == argument).Value;
        if (declared.Kind == TypedConstantKind.Array)
            foreach (var v in declared.Values)
            {
                var name = v.Value as string ?? "";
                var prop = props.FirstOrDefault(p => p.Name == name);
                if (prop is null) unknown.Add(name);
                else if (!columns.Contains(prop.Column)) columns.Add(prop.Column);
            }
        return (columns.ToImmutableArray(), unknown.ToImmutableArray());
    }

    private static string EntityToken(string typeName)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < typeName.Length; i++)
        {
            var ch = typeName[i];
            if (i > 0 && ch >= 'A' && ch <= 'Z' && ((typeName[i - 1] >= 'a' && typeName[i - 1] <= 'z') || (typeName[i - 1] >= '0' && typeName[i - 1] <= '9')))
                sb.Append('_');
            sb.Append(ch >= 'A' && ch <= 'Z' ? (char)(ch + 32) : ch);
        }
        return sb.ToString();
    }

    private static string SoftTenantArgs(Entity e)
    {
        if (e.SoftDeleteColumn is null || e.Soft is not { } soft) return "";
        var args = new List<string>();
        if (soft.Platform) args.Add("Platform: true");
        else if (soft.TenantProp is { } tenantProp) args.Add("TenantColumn: " + Quote(tenantProp.Column));
        if (soft.UnversionedColumns.Length > 0)
            args.Add("Unversioned: new string[] { " + string.Join(", ", soft.UnversionedColumns.Select(Quote)) + " }");
        if (SoleKey(e) is { } key) args.Add("KeyColumn: " + Quote(key.Column));
        if (soft.LabelProp is { Read: "String" } label) args.Add("LabelColumn: " + Quote(label.Column));
        if (soft.OwnerColumns.Length > 0)
            args.Add("Owners: new string[] { " + string.Join(", ", soft.OwnerColumns.Select(Quote)) + " }");
        if (soft.PinnedColumns.Length > 0)
            args.Add("PinnedColumns: new string[] { " + string.Join(", ", soft.PinnedColumns.Select(Quote)) + " }");
        if (soft.StatusColumns.Length > 0)
            args.Add("StatusColumns: new string[] { " + string.Join(", ", soft.StatusColumns.Select(Quote)) + " }");
        args.Add("EntityToken: " + Quote(EntityToken(e.Type)));
        args.Add("PropertyColumns: new string[][] { " + string.Join(", ", e.Props.Select(p => "new string[] { " + Quote(p.Name) + ", " + Quote(p.Column) + " }")) + " }");
        return args.Count == 0 ? "" : ", " + string.Join(", ", args);
    }

    private static Prop? SoleKey(Entity e)
    {
        var keys = e.Props.Where(p => p.IsKey).ToList();
        return keys.Count == 1 ? keys[0] : null;
    }

    private static string HistoryTableLit(Entity e)
    {
        if (e.SoftDeleteColumn is null || e.Soft is not { } soft || SoleKey(e) is not { } key) return "";
        if (!soft.Platform && soft.TenantProp is null) return "";
        var unversioned = soft.UnversionedColumns.Length == 0
            ? "new string[0]"
            : "new string[] { " + string.Join(", ", soft.UnversionedColumns.Select(Quote)) + " }";
        return "new global::Neemle.Orm.Lite.RowHistoryTable(" + string.Join(", ", new[]
        {
            Quote(e.Table), Quote(key.Column), Quote(SoftColumnName(e)), Quote(soft.Platform ? "" : soft.TenantProp!.Column), unversioned,
        }) + ")";
    }

    private static string SoftColumnName(Entity e) =>
        e.SoftDeleteColumn is { } name ? e.Props.First(p => p.Name == name).Column : "";

    private static string LivePredicate(Entity e) => $"\"{SoftColumnName(e)}\" IS NULL";

    private static string LiveOnlyIndexesArg(Entity e)
    {
        var sets = e.SoftDeleteColumn is null
            ? new List<string>()
            : e.Indexes.Where(ix => ix.LiveOnly).Select(ix => "new string[] { " + string.Join(", ", ix.Columns.Select(Quote)) + " }").ToList();
        return sets.Count == 0 ? "" : ", new string[][] { " + string.Join(", ", sets) + " }";
    }

    private static string LiveFilter(Entity e) =>
        e.SoftDeleteColumn is null ? "" : " AND " + LivePredicate(e);

    private static bool DeclaresSoftDelete(Entity e) => e.SoftDeleteColumn is not null || e.SoftDeleteIssue is not null;

    private static string ColumnOf(ISymbol m) => AttrArg(m, "Neemle.Orm.Lite.ColumnAttribute") ?? Snake(m.Name);

    private static (AttributeData Attribute, string Name, IPropertySymbol? Member)? SoftDeleteDeclaration(INamedTypeSymbol t)
    {
        var a = t.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == "Neemle.Orm.Lite.SoftDeleteAttribute");
        if (a is null) return null;
        var name = (a.ConstructorArguments.Length > 0 ? a.ConstructorArguments[0].Value as string : null) ?? "DeletedAt";
        return (a, name, t.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault());
    }

    private static string? SoftColumnOf(INamedTypeSymbol t) =>
        SoftDeleteDeclaration(t) is { Member: { } member } ? ColumnOf(member) : null;

    private static (string? Column, SoftDeleteIssue? Issue) ReadSoftDelete(INamedTypeSymbol s, List<Prop> props)
    {
        if (SoftDeleteDeclaration(s) is not (var a, var name, var member)) return (null, null);
        var location = a.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? s.Locations.FirstOrDefault() ?? Location.None;
        string? reason = null;
        if (member is null)
            reason = "is not a property of the entity";
        else if (!(member.Type is INamedTypeSymbol nt
                   && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
                   && nt.TypeArguments[0].SpecialType == SpecialType.System_DateTime))
            reason = "is of type '" + member.Type.ToDisplayString() + "', not 'System.DateTime?'";
        else if (!props.Any(p => p.Name == name))
            reason = "is excluded from the column set ([NotAColumn], or not a public read-write instance property)";
        return reason is null ? (name, null) : (null, new SoftDeleteIssue(name, reason, location));
    }

    private static Repo ReadRepo(INamedTypeSymbol s)
    {
        var methods = new List<Method>();
        foreach (var m in s.GetMembers().OfType<IMethodSymbol>())
        {
            var sqlAttr = m.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Neemle.Orm.Lite.SqlAttribute");
            if (sqlAttr is null || sqlAttr.ConstructorArguments.Length == 0) continue;
            var sql = sqlAttr.ConstructorArguments[0].Value as string ?? "";

            var ret = m.ReturnType as INamedTypeSymbol;
            var inner = ret is { IsGenericType: true } ? ret.TypeArguments[0] : null;
            string kind; string? entity = null;
            if (inner is null) { kind = "exec"; }
            else if (inner.SpecialType == SpecialType.System_String) kind = "scalarString";
            else if (inner.SpecialType == SpecialType.System_Boolean) kind = "bool";
            else if (inner.SpecialType == SpecialType.System_Int64) kind = "scalarLong";
            else if (inner.SpecialType == SpecialType.System_Int32) kind = "scalarInt";
            else if (inner.Name == "Guid" || (inner is INamedTypeSymbol ng && ng.Name == "Nullable" && ng.TypeArguments.Length == 1 && ng.TypeArguments[0].Name == "Guid")) kind = "scalarGuid";
            else if (inner is INamedTypeSymbol li && li.Name is "List" or "IReadOnlyList" or "IList" && li.TypeArguments.Length == 1)
            { kind = "list"; entity = li.TypeArguments[0].Name; }
            else { kind = "single"; entity = inner.Name; }

            var ps = m.Parameters.Select(p => new Param(p.Name, p.Type.ToDisplayString())).ToImmutableArray();
            methods.Add(new Method(m.Name, sql, kind, entity, m.ReturnType.ToDisplayString(), ps));
        }
        return new Repo(s.ContainingNamespace.ToDisplayString(), s.Name, methods.ToImmutableArray());
    }

    private static void EmitEntities(SourceProductionContext spc, ImmutableArray<Entity> entities, string asmToken)
    {
        var createAll = new List<string>();
        var createIndexes = new List<string>();
        var reconcileIndexes = new List<string>();
        var emitted = new List<object>();
        var byType = entities.ToDictionary(x => x.Type, x => x);
        var allTables = new HashSet<string>(entities.Select(x => x.Table));
        foreach (var e in entities)
            foreach (var u in e.Unmapped)
                spc.ReportDiagnostic(Diagnostic.Create(UnmappedPropertyType, u.Location, e.Type, u.Property, u.TypeName));
        foreach (var e in entities)
            if (e.SoftDeleteIssue is { } issue)
                spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftDeleteColumn, issue.Location, e.Type, issue.Property, issue.Reason));
        foreach (var e in entities)
            ReportIndexDeclarations(spc, e);
        foreach (var e in entities)
            ReportSoftDeclaration(spc, e);
        foreach (var e in entities)
        {
            var full = $"global::{e.Namespace}.{e.Type}";
            var cols = string.Join(", ", e.Props.Select(p =>
                $"\"{p.Column}\" {p.SqlType}{(p.Nullable ? "" : " NOT NULL")}{(p.IsKey ? " PRIMARY KEY" : DefaultFor(p))}"));
            if (e.Managed)
            {
                createAll.Add($"CREATE TABLE IF NOT EXISTS \"{e.Table}\" ({cols})");
                foreach (var ix in e.Indexes)
                {
                    var liveOnly = ix.LiveOnly && e.SoftDeleteColumn is not null;
                    var live = liveOnly ? " WHERE " + LivePredicate(e) : "";
                    var stmt = $"CREATE {(ix.Unique ? "UNIQUE " : "")}INDEX IF NOT EXISTS \"{ix.Name}\" ON \"{e.Table}\" ({string.Join(", ", ix.Columns.Select(c => $"\"{c}\""))}){live}";
                    createIndexes.Add(stmt);
                    reconcileIndexes.Add(GuardIndex(stmt, e.Table, liveOnly ? GuardedColumns(ix.Columns, SoftColumnName(e)) : ix.Columns));
                    if (liveOnly) reconcileIndexes.Add(CollisionCheck(e.Table, ix.Name, ix.Columns, GuardedColumns(ix.Columns, SoftColumnName(e))));
                }
            }

            var insCols = string.Join(", ", e.Props.Select(p => $"\"{p.Column}\""));
            var insVals = string.Join(", ", e.Props.Select((p, i) => Placeholder(p, "p" + i)));
            var insertSql = $"INSERT INTO \"{e.Table}\" ({insCols}) VALUES ({insVals})";

            var key = e.Props.FirstOrDefault(p => p.IsKey);
            var setProps = key is null ? System.Array.Empty<Prop>() : e.Props.Where(p => !p.IsKey && p.Name != e.SoftDeleteColumn).ToArray();
            var sets = string.Join(", ", setProps.Select((p, i) => $"\"{p.Column}\" = {Placeholder(p, "s" + i)}"));

            emitted.Add(new
            {
                e.Type,
                e.Table,
                Full = full,
                e.Access,
                Props = e.Props.Select(p => new
                {
                    p.Name,
                    p.Column,
                    p.Nullable,
                    ReadO = ReadExpr(p, "o", e.Table, key?.Column ?? ""),
                    ReadOrdinal = ReadExpr(p, $"r.GetOrdinal(\"{p.Column}\")", e.Table, key?.Column ?? ""),
                }).ToList(),
                InsertSqlLit = Quote(insertSql),
                InsertParams = e.Props.Select((p, i) => new { P = $"p{i}", Bind = BindValue(p) }).ToList(),
                SoftDeleteColumn = e.SoftDeleteColumn ?? "",
                SoftDeleteColumnName = SoftColumnName(e),
                LiveOnlyIndexesArg = LiveOnlyIndexesArg(e),
                SoftTenantArgs = SoftTenantArgs(e),
                KeyColumn = key?.Column ?? "",
                HasKey = key is not null,
                KeyCs = key?.Cs ?? "",
                GetSqlLit = key is null ? "" : Quote($"SELECT * FROM \"{e.Table}\" WHERE \"{key.Column}\" = @k{LiveFilter(e)}"),
                DeleteSqlLit = key is null || e.SoftDeleteColumn is not null ? "" : Quote($"DELETE FROM \"{e.Table}\" WHERE \"{key.Column}\" = @k"),
                HasSetProps = setProps.Length > 0,
                UpdateSqlLit = setProps.Length == 0 ? "" : Quote($"UPDATE \"{e.Table}\" SET {sets} WHERE \"{key!.Column}\" = @key{LiveFilter(e)}"),
                UpdateParams = setProps.Select((p, i) => new { P = $"s{i}", Bind = BindValue(p) }).ToList(),
                KeyBindForUpdate = key is null ? "" : BindValue(key),
                Relations = e.Relations.Select(rel => new
                {
                    rel.Nav,
                    LoadBody = rel.FkNullable
                        ? $"e.{rel.Nav} = e.{rel.FkProp} is null ? null : await {rel.TargetType}Orm.GetAsync(conn, e.{rel.FkProp}.Value);"
                        : $"e.{rel.Nav} = await {rel.TargetType}Orm.GetAsync(conn, e.{rel.FkProp});",
                }).ToList(),
                Collections = e.Collections.Select(col => new { col.Nav, col.ChildType, col.InverseFk, col.ParentKey }).ToList(),
                ManyToMany = e.ManyToMany.Select(mm => new
                {
                    mm.Nav,
                    mm.OtherType,
                    mm.OtherFull,
                    ThisKeyProp = key?.Name ?? "Id",
                    ThisKeyCs = key?.Cs ?? "global::System.Guid",
                    mm.OtherKeyCs,
                    Singular = mm.Nav.EndsWith("s") ? mm.Nav.Substring(0, mm.Nav.Length - 1) : mm.Nav,
                    Pure = !allTables.Contains(mm.JoinTable),
                    LoadSqlLit = Quote($"SELECT o.* FROM \"{mm.OtherTable}\" o JOIN \"{mm.JoinTable}\" j ON j.\"{mm.OtherKey}\" = o.\"{mm.OtherKeyCol}\" WHERE j.\"{mm.ThisKey}\" = @k{(mm.OtherSoftColumn is null ? "" : $" AND o.\"{mm.OtherSoftColumn}\" IS NULL")}"),
                    AddSqlLit = Quote($"INSERT INTO \"{mm.JoinTable}\" (\"{mm.ThisKey}\", \"{mm.OtherKey}\") VALUES (@a, @b) ON CONFLICT DO NOTHING"),
                    RemoveSqlLit = Quote($"DELETE FROM \"{mm.JoinTable}\" WHERE \"{mm.ThisKey}\" = @a AND \"{mm.OtherKey}\" = @b"),
                }).ToList(),
            });
            if (e.Managed)
                foreach (var mm in e.ManyToMany.Where(mm => !allTables.Contains(mm.JoinTable)))
                    createAll.Add($"CREATE TABLE IF NOT EXISTS \"{mm.JoinTable}\" (\"{mm.ThisKey}\" uuid NOT NULL, \"{mm.OtherKey}\" uuid NOT NULL, PRIMARY KEY (\"{mm.ThisKey}\", \"{mm.OtherKey}\"))");
        }

        var model = new
        {
            Entities = emitted,
            AsmToken = asmToken,
            CreateAllLit = Quote(string.Join("; ", createAll)),
            CreateIndexesLit = string.Join(", ", createIndexes.Select(Quote)),
            ReconcileIndexesLit = string.Join(", ",
                new[] { Quote("SET LOCAL lock_timeout = '5s'") }.Concat(reconcileIndexes.Select(Quote))),
            IndexSetHash = StableHash(createIndexes),
            HasHistory = entities.Any(e => e.Soft is not null),
            HistoryTablesLit = string.Join(", ", entities.OrderBy(e => e.Table, System.StringComparer.Ordinal)
                .Select(HistoryTableLit).Where(lit => lit.Length > 0)),
        };
        var code = global::Neemle.Orm.Lite.Templating.Template.RenderResource(
            typeof(OrmGenerator).Assembly, "Entities.cs-template", model);
        spc.AddSource("OrmLite.Entities.g.cs", code);
    }

    private static void EmitRepos(SourceProductionContext spc, ImmutableArray<Repo> repos, ImmutableArray<Entity> entities)
    {
        var byName = entities.ToDictionary(e => e.Type, e => e);
        foreach (var repo in repos)
        {
            var model = new
            {
                repo.Namespace,
                repo.Type,
                Methods = repo.Methods.Select(m => new
                {
                    m.ReturnType,
                    m.Name,
                    ParamSig = string.Join(", ", m.Params.Select(p => $"{p.Type} {p.Name}")),
                    SqlLit = Quote(m.Sql),
                    Params = m.Params.Select(p => new { p.Name }).ToList(),
                    IsBool = m.Kind == "bool",
                    IsScalarString = m.Kind == "scalarString",
                    IsScalarLong = m.Kind == "scalarLong",
                    IsScalarInt = m.Kind == "scalarInt",
                    IsScalarGuid = m.Kind == "scalarGuid",
                    IsSingle = m.Kind == "single",
                    IsList = m.Kind == "list",
                    IsExec = m.Kind != "bool" && m.Kind != "scalarString" && m.Kind != "scalarLong"
                             && m.Kind != "scalarInt" && m.Kind != "single" && m.Kind != "list" && m.Kind != "scalarGuid",
                    IsSelect = IsSelect(m.Sql),
                    Entity = m.Entity ?? "",
                    ListType = m.Entity is not null && byName.TryGetValue(m.Entity, out var en) ? $"{en.Namespace}.{m.Entity}" : "",
                }).ToList(),
            };
            var code = global::Neemle.Orm.Lite.Templating.Template.RenderResource(
                typeof(OrmGenerator).Assembly, "Repo.cs-template", model);
            spc.AddSource($"OrmLite.{repo.Type}.g.cs", code);
        }
    }

    private static string ReadExpr(Prop p, string ord, string table, string keyColumn) => p.Read switch
    {
        "Int32" => $"r.GetInt32({ord})",
        "Int64" => $"r.GetInt64({ord})",
        "Boolean" => $"r.GetBoolean({ord})",
        "Double" => $"r.GetDouble({ord})",
        "Decimal" => $"r.GetDecimal({ord})",
        "DateTime" => $"r.GetDateTime({ord})",
        "Date" => $"r.GetFieldValue<global::System.DateOnly>({ord})",
        "Guid" => $"r.GetGuid({ord})",
        "Enum" => $"global::Neemle.Orm.Lite.OrmEnum.Read<{p.Cs}>(r, {ord}, \"{table}\", \"{p.Column}\", \"{keyColumn}\")",
        "Jsonb" => $"global::Neemle.Orm.Lite.Jsonb.FromStorage(r.GetString({ord}))",
        _ => $"r.GetString({ord})",
    };

    private static string BindValue(Prop p) =>
        p.Read == "Enum" ? (p.Nullable ? $"e.{p.Name}?.ToString()" : $"e.{p.Name}.ToString()")
        : p.Read == "Jsonb" ? $"e.{p.Name}?.Json"
        : $"e.{p.Name}";

    private static string Placeholder(Prop p, string name) =>
        p.SqlType == JsonbSqlType ? "@" + name + "::" + JsonbSqlType : "@" + name;

    public const string JsonbClrTypeName = "Neemle.Orm.Lite.Jsonb";

    public const string JsonbSqlType = "jsonb";

    public const string JsonbEmptyDocumentDefaultSql = "'{}'::jsonb";

    private static readonly DiagnosticDescriptor UnmappedPropertyType = new DiagnosticDescriptor(
        id: "ORMLITE001",
        title: "Orm.Lite has no column mapping for this property's type",
        messageFormat: "Orm.Lite has no column mapping for property '{0}.{1}' of type '{2}'. Add a mapping to OrmGenerator.Classify, or type the property as Neemle.Orm.Lite.Jsonb to store it as a jsonb column. The generator no longer falls back to a text column, so this property is not emitted.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftDeleteColumn = new DiagnosticDescriptor(
        id: "ORMLITE002",
        title: "A [SoftDelete] column must be a mapped System.DateTime? property",
        messageFormat: "[SoftDelete] on '{0}' names '{1}', which {2}. The soft-delete column must be a mapped System.DateTime? property of the entity.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidLiveOnlyIndex = new DiagnosticDescriptor(
        id: "ORMLITE004",
        title: "A LiveOnly index must be a Unique index on a [SoftDelete] entity",
        messageFormat: "Index '{1}' on '{0}' is declared LiveOnly but {2}. A LiveOnly index is a partial unique index over the live rows of a [SoftDelete] entity.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateIndexName = new DiagnosticDescriptor(
        id: "ORMLITE005",
        title: "An explicit index Name must not repeat another index of the same entity",
        messageFormat: "Index name '{1}' on '{0}' is declared explicitly and is already the name of another index on the same table; CREATE INDEX IF NOT EXISTS would silently skip one of them. Give each index its own name.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftTenant = new DiagnosticDescriptor(
        id: "ORMLITE007",
        title: "A [SoftDelete] entity needs a Guid tenant column or Tenant = Platform",
        messageFormat: "[SoftDelete] on '{0}' has no usable tenant: Tenant '{1}' {2}. Declare a System.Guid tenant property ({3} by default, or name it with Tenant = ...), or declare Tenant = SoftDeleteAttribute.Platform for an entity with no tenant.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidDefaultTenant = new DiagnosticDescriptor(
        id: "ORMLITE014",
        title: "OrmLiteDefaultTenant must be a C# identifier",
        messageFormat: "The OrmLiteDefaultTenant build property is '{0}', which is not a C# identifier. Set it to the name of the System.Guid property that a [SoftDelete] entity carries as its tenant by default, for example TenantId.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftKey = new DiagnosticDescriptor(
        id: "ORMLITE008",
        title: "A [SoftDelete] entity needs exactly one System.Guid key",
        messageFormat: "[SoftDelete] on '{0}' requires exactly one non-nullable System.Guid key property, but {1}",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftUnversioned = new DiagnosticDescriptor(
        id: "ORMLITE009",
        title: "A [SoftDelete] Unversioned name must be a mapped column",
        messageFormat: "[SoftDelete] on '{0}' lists '{1}' in Unversioned, which is not a mapped property of the entity. A misspelled name would leave the column in the history rows.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftReference = new DiagnosticDescriptor(
        id: "ORMLITE010",
        title: "A [SoftDelete] Label or Owners name must be a mapped column",
        messageFormat: "[SoftDelete] on '{0}' names '{2}' in {1}, which is not a mapped property of the entity",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftLabelType = new DiagnosticDescriptor(
        id: "ORMLITE011",
        title: "A [SoftDelete] Label must be a text column",
        messageFormat: "[SoftDelete] on '{0}' names '{1}' as its Label, which is of type '{2}', not string. A label is a text column.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftColumnList = new DiagnosticDescriptor(
        id: "ORMLITE012",
        title: "A [SoftDelete] Pinned or StatusColumns name must be a mapped column",
        messageFormat: "[SoftDelete] on '{0}' names '{2}' in {1}, which is not a mapped property of the entity",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor StatusColumnIsPinned = new DiagnosticDescriptor(
        id: "ORMLITE013",
        title: "A [SoftDelete] StatusColumns name must not also be Pinned",
        messageFormat: "[SoftDelete] on '{0}' lists '{1}' in both Pinned and StatusColumns. A column the restore keeps cannot also be one the human's option governs.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static ImmutableArray<Entity> ResolveTenants(ImmutableArray<Entity> entities, string option)
    {
        var name = option.Length > 0 && IsIdentifier(option) ? option : DefaultTenant;
        return entities.Select(e => e.Soft is not { } soft ? e : e with
        {
            Soft = soft with
            {
                DefaultName = name,
                TenantName = soft.TenantDeclared ? soft.TenantName : name,
                TenantProp = soft.TenantDeclared ? soft.TenantProp : e.Props.FirstOrDefault(p => p.Name == name),
            },
        }).ToImmutableArray();
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_')) return false;
        foreach (var ch in value)
            if (!(char.IsLetterOrDigit(ch) || ch == '_')) return false;
        return Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(value) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None;
    }

    private static void ReportDefaultTenant(SourceProductionContext spc, string value)
    {
        if (value.Length > 0 && !IsIdentifier(value))
            spc.ReportDiagnostic(Diagnostic.Create(InvalidDefaultTenant, Location.None, value));
    }

    private static void ReportSoftDeclaration(SourceProductionContext spc, Entity e)
    {
        if (e.SoftDeleteColumn is null || e.Soft is not { } soft) return;
        if (!soft.Platform)
        {
            string? tenantReason = null;
            if (soft.TenantProp is null) tenantReason = "is not a mapped property of the entity";
            else if (soft.TenantProp.Read != "Guid") tenantReason = "is of type '" + soft.TenantProp.Cs + "', not System.Guid";
            if (tenantReason is not null)
                spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftTenant, soft.Location, e.Type, soft.TenantName, tenantReason, soft.DefaultName));
        }
        var keys = e.Props.Where(p => p.IsKey).ToList();
        string? keyReason = null;
        if (keys.Count == 0) keyReason = "it has none";
        else if (keys.Count > 1) keyReason = "it has " + keys.Count + " (" + string.Join(", ", keys.Select(k => k.Name)) + ")";
        else if (keys[0].Read != "Guid" || keys[0].Nullable) keyReason = "'" + keys[0].Name + "' is not a non-nullable System.Guid";
        if (keyReason is not null)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftKey, soft.Location, e.Type, keyReason));
        foreach (var name in soft.UnknownUnversioned)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftUnversioned, soft.Location, e.Type, name));
        if (soft.LabelName is not null && soft.LabelProp is null)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftReference, soft.Location, e.Type, "Label", soft.LabelName));
        foreach (var name in soft.UnknownOwners)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftReference, soft.Location, e.Type, "Owners", name));
        if (soft.LabelProp is { Read: not "String" } label)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftLabelType, soft.Location, e.Type, label.Name, label.Cs));
        foreach (var name in soft.UnknownPinned)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftColumnList, soft.Location, e.Type, "Pinned", name));
        foreach (var name in soft.UnknownStatus)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftColumnList, soft.Location, e.Type, "StatusColumns", name));
        foreach (var column in soft.StatusPinnedOverlap)
            spc.ReportDiagnostic(Diagnostic.Create(StatusColumnIsPinned, soft.Location, e.Type, e.Props.First(p => p.Column == column).Name));
    }

    private static void ReportIndexDeclarations(SourceProductionContext spc, Entity e)
    {
        foreach (var ix in e.Indexes)
        {
            var location = ix.Location ?? Location.None;
            var reasons = new List<string>();
            if (ix.LiveOnly && !DeclaresSoftDelete(e)) reasons.Add("the entity is not [SoftDelete]");
            if (ix.LiveOnly && !ix.Unique) reasons.Add("the index is not Unique");
            if (reasons.Count > 0)
                spc.ReportDiagnostic(Diagnostic.Create(InvalidLiveOnlyIndex, location, e.Type, ix.Name, string.Join(" and ", reasons)));
            if (ix.ExplicitName && e.Indexes.Count(o => o.Name == ix.Name) > 1)
                spc.ReportDiagnostic(Diagnostic.Create(DuplicateIndexName, location, e.Type, ix.Name));
        }
    }

    private static bool IsUnresolved(ITypeSymbol t)
    {
        var u = t;
        if (t is INamedTypeSymbol nt && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            u = nt.TypeArguments[0];
        return u.TypeKind == TypeKind.Error;
    }

    private static (string read, string sql, string cs)? Classify(ITypeSymbol t)
    {
        var u = t;
        if (t is INamedTypeSymbol nt && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            u = nt.TypeArguments[0];
        if (u.TypeKind == TypeKind.Enum) return ("Enum", "text", "global::" + u.ToDisplayString());
        var display = u.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString();
        if (display == "System.Guid") return ("Guid", "uuid", "global::System.Guid");
        if (display == "System.DateOnly") return ("Date", "date", "global::System.DateOnly");
        if (display == "System.DateTime") return ("DateTime", "timestamp with time zone", "global::System.DateTime");
        if (display == JsonbClrTypeName) return ("Jsonb", JsonbSqlType, "global::" + JsonbClrTypeName);
        return u.SpecialType switch
        {
            SpecialType.System_String => ("String", "text", "string"),
            SpecialType.System_Int32 => ("Int32", "integer", "int"),
            SpecialType.System_Int64 => ("Int64", "bigint", "long"),
            SpecialType.System_Boolean => ("Boolean", "boolean", "bool"),
            SpecialType.System_Double => ("Double", "double precision", "double"),
            SpecialType.System_Decimal => ("Decimal", "numeric(12,4)", "decimal"),
            _ => ((string, string, string)?)null,
        };
    }

    private static string DefaultFor(Prop p)
    {
        if (p.Nullable) return "";
        return p.SqlType switch
        {
            "text" => " DEFAULT ''::text",
            "boolean" => " DEFAULT false",
            "integer" or "bigint" => " DEFAULT 0",
            "double precision" => " DEFAULT 0",
            "timestamp with time zone" => " DEFAULT now()",
            JsonbSqlType => " DEFAULT " + JsonbEmptyDocumentDefaultSql,
            _ => "",
        };
    }

    private static System.Collections.Generic.IEnumerable<string> GuardedColumns(System.Collections.Generic.IEnumerable<string> keys, string predicateColumn)
    {
        var list = keys.ToArray();
        return list.Contains(predicateColumn, System.StringComparer.Ordinal) ? list : list.Append(predicateColumn);
    }

    private static string GuardIndex(string createIndexSql, string table, System.Collections.Generic.IEnumerable<string> columns)
    {
        return "DO $reconcile$ BEGIN IF " + GuardCondition(table, columns)
             + " THEN EXECUTE '" + createIndexSql + "'; END IF; END $reconcile$";
    }

    private static string GuardCondition(string table, System.Collections.Generic.IEnumerable<string> columns)
    {
        var cols = columns.ToArray();
        var list = string.Join(", ", cols.Select(c => "'" + c + "'"));
        return "to_regclass('\"" + table + "\"') IS NOT NULL"
             + " AND (SELECT count(*) FROM pg_attribute"
             + " WHERE attrelid = to_regclass('\"" + table + "\"') AND attnum > 0 AND NOT attisdropped"
             + " AND attname = ANY (ARRAY[" + list + "])) = " + cols.Length;
    }

    private static string CollisionCheck(string table, string liveIndex, System.Collections.Generic.IEnumerable<string> keyColumns, System.Collections.Generic.IEnumerable<string> guardedColumns)
    {
        var keys = keyColumns.ToArray();
        var list = string.Join(", ", keys.Select(c => "'" + c + "'"));
        return "DO $collision$ DECLARE r record; BEGIN IF " + GuardCondition(table, guardedColumns) + " THEN"
             + " FOR r IN SELECT ic.relname::text AS idx, c.conname::text AS con"
             + " FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid"
             + " LEFT JOIN pg_constraint c ON c.conindid = i.indexrelid AND c.contype IN ('u', 'p')"
             + " WHERE i.indrelid = to_regclass('\"" + table + "\"') AND i.indisunique AND i.indpred IS NULL AND i.indexprs IS NULL"
             + " AND i.indnkeyatts = " + keys.Length
             + " AND (SELECT count(*) FROM unnest(i.indkey::int2[]) WITH ORDINALITY AS k(attnum, ord)"
             + " JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum"
             + " WHERE k.ord <= i.indnkeyatts AND a.attname = ANY (ARRAY[" + list + "])) = " + keys.Length
             + " LOOP RAISE EXCEPTION 'Live-only index % on table % over columns (%) cannot take effect: % % is unique over the same columns without a live-row predicate',"
             + " '" + liveIndex + "', '" + table + "', '" + string.Join(", ", keys) + "',"
             + " CASE WHEN r.con IS NULL THEN 'index' ELSE 'constraint' END, COALESCE(r.con, r.idx)"
             + " USING HINT = 'Convert it with an explicit migration: create the partial index, then drop the total one.';"
             + " END LOOP; END IF; END $collision$";
    }

    private static string StableHash(System.Collections.Generic.IEnumerable<string> statements)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL, prime = 1099511628211UL;
            var h = offset;
            foreach (var s in statements.OrderBy(x => x, System.StringComparer.Ordinal))
            {
                foreach (var ch in s) { h ^= ch; h *= prime; }
                h ^= '\n'; h *= prime;
            }
            return h.ToString("x16");
        }
    }

    private static (string Table, string KeyCol, string KeyCs, string Full) EntityMeta(INamedTypeSymbol t)
    {
        var table = AttrArg(t, "Neemle.Orm.Lite.TableAttribute") ?? Snake(t.Name);
        var keyProp = t.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => HasAttr(p, "Neemle.Orm.Lite.KeyAttribute"))
                      ?? t.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => p.Name == "Id");
        var keyCol = keyProp is null ? "id" : ColumnOf(keyProp);
        var keyCs = keyProp is null ? "global::System.Guid" : (Classify(keyProp.Type)?.cs ?? "global::System.Guid");
        return (table, keyCol, keyCs, "global::" + t.ToDisplayString());
    }

    private static bool HasAttr(ISymbol s, string name) =>
        s.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);

    private static bool NamedBool(AttributeData a, string name) =>
        a.NamedArguments.Any(n => n.Key == name && n.Value.Value is true);

    private static bool NamedBoolOrDefault(ISymbol s, string attribute, string name, bool fallback)
    {
        var a = s.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == attribute);
        if (a is null) return fallback;
        foreach (var n in a.NamedArguments)
            if (n.Key == name && n.Value.Value is bool b) return b;
        return fallback;
    }

    private static bool IsSelect(string sql) =>
        sql.TrimStart().StartsWith("SELECT", System.StringComparison.OrdinalIgnoreCase);

    private static string? AttrArg(ISymbol s, string name)
    {
        var a = s.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == name);
        return a is { ConstructorArguments.Length: > 0 } ? a.ConstructorArguments[0].Value as string : null;
    }

    private static string Snake(string s)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (char.IsUpper(ch)) { if (i > 0) sb.Append('_'); sb.Append(char.ToLowerInvariant(ch)); }
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private sealed record SoftDeclaration(string TenantName, bool TenantDeclared, string DefaultName, bool Platform, Prop? TenantProp, ImmutableArray<string> UnversionedColumns, ImmutableArray<string> UnknownUnversioned, Location Location, string? LabelName, Prop? LabelProp, ImmutableArray<string> OwnerColumns, ImmutableArray<string> UnknownOwners, ImmutableArray<string> PinnedColumns, ImmutableArray<string> UnknownPinned, ImmutableArray<string> StatusColumns, ImmutableArray<string> UnknownStatus, ImmutableArray<string> StatusPinnedOverlap);
    private sealed record Entity(string Namespace, string Type, string Table, ImmutableArray<Prop> Props, ImmutableArray<IndexDef> Indexes, ImmutableArray<Relation> Relations, ImmutableArray<Collection> Collections, ImmutableArray<ManyToMany> ManyToMany, bool Managed, ImmutableArray<Unmapped> Unmapped, string? SoftDeleteColumn, SoftDeleteIssue? SoftDeleteIssue, SoftDeclaration? Soft, string Access);
    private sealed record Unmapped(string Property, string TypeName, Location Location);
    private sealed record SoftDeleteIssue(string Property, string Reason, Location Location);
    private sealed record IndexDef(string Name, ImmutableArray<string> Columns, bool Unique, bool LiveOnly = false, bool ExplicitName = false, Location? Location = null);
    private sealed record Relation(string Nav, string TargetType, string FkProp, bool FkNullable);
    private sealed record Collection(string Nav, string ChildType, string InverseFk, string ParentKey);
    private sealed record ManyToMany(string Nav, string OtherType, string OtherFull, string OtherTable, string OtherKeyCol, string OtherKeyCs, string JoinTable, string ThisKey, string OtherKey, string? OtherSoftColumn);
    private sealed record Prop(string Name, string Column, string SqlType, string Read, bool IsKey, bool Nullable, string Cs);
    private sealed record Repo(string Namespace, string Type, ImmutableArray<Method> Methods);
    private sealed record Method(string Name, string Sql, string Kind, string? Entity, string ReturnType, ImmutableArray<Param> Params);
    private sealed record Param(string Name, string Type);
}
