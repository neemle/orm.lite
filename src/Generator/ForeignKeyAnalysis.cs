using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using ManyToMany = Neemle.OrmLite.Generator.OrmGenerator.ManyToMany;

namespace Neemle.OrmLite.Generator;

internal sealed class ForeignKeyAnalysis
{
    private static readonly DiagnosticDescriptor Invalid = new("ORMLITE035", "Invalid foreign key declaration",
        "Foreign key on '{0}.{1}': {2}. The generated schema baseline is withheld.", "OrmLite",
        DiagnosticSeverity.Error, true);

    private readonly SourceProductionContext _context;
    private readonly ProviderSet _providers;
    private readonly ImmutableArray<Entity> _entities;
    private readonly Dictionary<string, Entity> _byFull;
    private readonly HashSet<(string Entity, string Nav)> _blocked;
    private readonly Dictionary<string, List<ForeignKeyModel>> _keys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Table, ForeignKeyModel Key)> _names =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _parents = new(StringComparer.Ordinal);
    private bool _valid = true;

    internal ForeignKeyAnalysis(SourceProductionContext context, ImmutableArray<Entity> entities,
        HashSet<(string Entity, string Nav)> blocked, ProviderSet providers)
    {
        _context = context;
        _providers = providers;
        _entities = entities;
        _blocked = blocked;
        _byFull = entities.GroupBy(e => e.Full).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    internal bool Valid => _valid;

    internal ImmutableArray<Entity> Resolve()
    {
        if (!_entities.Any(e => !e.ForeignKeyIntents.IsEmpty)) return _entities;
        ValidateTables();
        ValidateJoins();
        foreach (var entity in _entities)
            foreach (var intent in entity.ForeignKeyIntents) Resolve(entity, intent);
        CheckCycles();
        return _entities.Select(e => e with
        {
            ForeignKeys = _valid && _keys.TryGetValue(e.Table, out var keys)
                ? keys.OrderBy(k => k.Name, StringComparer.Ordinal).ToImmutableArray() : default,
            ForeignKeyIntents = _valid ? e.ForeignKeyIntents : default,
        }).ToImmutableArray();
    }

    private void ValidateTables()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _entities)
        {
            if (!IdentifierDiagnostics.ForeignKeyNamesFit(e, _providers)) _valid = false;
            if (e.Managed && OrmGenerator.Refused(e)) Error(e, null, "UnsupportedKey");
            if (!ValidName(e.Table) || !names.Add(e.Table)) Error(e, null, "InvalidName or ConflictingDeclaration");
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in e.Props)
                if (!ValidName(p.Column) || !columns.Add(p.Column)) Error(e, null, "UnmappedColumn or InvalidName");
            _parents.TryAdd(e.Table, new HashSet<string>(StringComparer.Ordinal));
        }
    }

    private void ValidateJoins()
    {
        var joins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in _entities.Where(e => e.Managed))
            foreach (var link in entity.ManyToMany) ValidateJoin(entity, link, joins);
    }

    private void ValidateJoin(Entity entity, ManyToMany link, Dictionary<string, string> joins)
    {
        var mapped = _entities.FirstOrDefault(e =>
            string.Equals(e.Table, link.JoinTable, StringComparison.OrdinalIgnoreCase));
        if (mapped is not null)
        {
            if (mapped.Table != link.JoinTable) Error(entity, null, "UnsupportedLink");
            return;
        }
        if (!ValidName(link.JoinTable) || !ValidName(link.ThisKey) || !ValidName(link.OtherKey))
            Error(entity, null, "InvalidName");
        var columns = new[] { link.ThisKey, link.OtherKey }.OrderBy(c => c, StringComparer.Ordinal);
        var identity = link.JoinTable + "\0" + string.Join("\0", columns);
        if (joins.TryGetValue(link.JoinTable, out var prior) && prior != identity)
            Error(entity, null, "UnsupportedLink");
        if (string.Equals(link.ThisKey, link.OtherKey, StringComparison.OrdinalIgnoreCase))
            Error(entity, null, "UnmappedColumn");
        joins[link.JoinTable] = identity;
    }

    private void Resolve(Entity entity, ForeignKeyIntent intent)
    {
        if (!ValidName(intent.Name)) { Error(entity, intent, "InvalidName"); return; }
        if (intent.Action is < 0 or > 3) { Error(entity, intent, "InvalidDeleteAction"); return; }
        if (intent.End is not null) { Link(entity, intent); return; }
        var relation = entity.Relations.FirstOrDefault(r => r.Nav == intent.Navigation);
        if (relation is not null)
        {
            Reference(entity, relation.TargetFull, relation.FkProp, intent);
            return;
        }
        var inverse = entity.Collections.FirstOrDefault(c => c.Nav == intent.Navigation);
        if (inverse is not null && Endpoint(entity, inverse.ChildFull, intent) is { } child)
        {
            Reference(child, entity.Full, inverse.InverseFk, intent);
            return;
        }
        Error(entity, intent, "UnknownRelation");
    }

    private Entity? Endpoint(Entity owner, string full, ForeignKeyIntent intent)
    {
        if (!_byFull.TryGetValue(full, out var target))
        { Error(owner, intent, "ExternalBaseline"); return null; }
        if (!owner.Managed || owner.IsView || !target.Managed || target.IsView)
        { Error(owner, intent, "UnmanagedEndpoint"); return null; }
        if (OrmGenerator.Dropped(owner) || OrmGenerator.Dropped(target))
        { Error(owner, intent, "UnknownRelation"); return null; }
        return target;
    }

    private Prop? Key(Entity owner, Entity target, ForeignKeyIntent intent)
    {
        var keys = target.Props.Where(p => p.IsKey).ToArray();
        if (keys.Length == 1 && !keys[0].Nullable && target.LostKeys.IsEmpty) return keys[0];
        Error(owner, intent, "UnsupportedKey");
        return null;
    }

    private void Reference(Entity owner, string targetFull, string property, ForeignKeyIntent intent)
    {
        if (OrmGenerator.Refused(owner)) { Error(owner, intent, "UnsupportedKey"); return; }
        var target = Endpoint(owner, targetFull, intent);
        if (target is null) return;
        var primary = Key(owner, target, intent);
        if (primary is null) return;
        var source = owner.Props.FirstOrDefault(p => p.Name == property);
        if (source is null) { Error(owner, intent, "UnmappedColumn"); return; }
        if (Shape(source) != Shape(primary))
        { Error(owner, intent, "IncompatibleKey"); return; }
        if (intent.Action == 3 && !source.Nullable)
        { Error(owner, intent, "NonNullableSetNull"); return; }
        if (_blocked.Contains((owner.Full, intent.Navigation)))
        { Error(owner, intent, "UnknownRelation: navigation is not loadable"); return; }
        Add(owner.Table, new ForeignKeyModel(intent.Name, source.Column, target.Table, primary.Column, intent.Action),
            owner, intent);
    }

    private static (string Clr, string Type, int Precision, int Scale) Shape(Prop property) =>
        (property.Cs, property.Type, property.Precision == 0 && property.Type == "Decimal" ? 12 : property.Precision,
            property.Precision == 0 && property.Type == "Decimal" ? 4 : property.Scale);

    private void Link(Entity owner, ForeignKeyIntent intent)
    {
        var link = owner.ManyToMany.FirstOrDefault(l => l.Nav == intent.Navigation);
        if (link is null) { Error(owner, intent, "UnknownRelation"); return; }
        var target = Endpoint(owner, link.OtherFull, intent);
        if (target is null) return;
        var keys = LinkKeys(owner, target, intent);
        if (keys is not { } pair) return;
        if (!SupportedLink(link, intent, pair)) { Error(owner, intent, "UnsupportedLink"); return; }
        if (LinkIssue(owner, target, link, intent) is { } issue) { Error(owner, intent, issue); return; }
        var endpoint = intent.End == 0 ? owner : target;
        var column = intent.End == 0 ? link.ThisKey : link.OtherKey;
        var primary = intent.End == 0 ? pair.Own : pair.Other;
        Add(link.JoinTable, new ForeignKeyModel(intent.Name, column, endpoint.Table, primary.Column, intent.Action),
            owner, intent);
    }

    private (Prop Own, Prop Other)? LinkKeys(Entity owner, Entity target, ForeignKeyIntent intent)
    {
        var own = Key(owner, owner, intent);
        var other = Key(owner, target, intent);
        return own is null || other is null ? null : (own, other);
    }

    private bool SupportedLink(ManyToMany link, ForeignKeyIntent intent, (Prop Own, Prop Other) keys) =>
        intent.End is 0 or 1 && keys.Own.Cs == "global::System.Guid" && keys.Other.Cs == "global::System.Guid"
        && !_entities.Any(e => string.Equals(e.Table, link.JoinTable, StringComparison.OrdinalIgnoreCase));

    private static string? LinkIssue(Entity owner, Entity target, ManyToMany link, ForeignKeyIntent intent)
    {
        if (owner.Full == target.Full) return "DependencyCycle";
        if (intent.Action == 3) return "NonNullableSetNull";
        return !ValidName(link.JoinTable) || !ValidName(link.ThisKey) || !ValidName(link.OtherKey) ? "InvalidName" : null;
    }

    private void Add(string table, ForeignKeyModel key, Entity declaration, ForeignKeyIntent intent)
    {
        if (!_keys.TryGetValue(table, out var keys)) _keys.Add(table, keys = new List<ForeignKeyModel>());
        var same = keys.FirstOrDefault(k => k.Column == key.Column
            && k.TargetTable == key.TargetTable && k.TargetColumn == key.TargetColumn);
        if (same is not null)
        {
            if (same != key) Error(declaration, intent, "ConflictingDeclaration");
            return;
        }
        if (_names.ContainsKey(key.Name)) { Error(declaration, intent, "DuplicateName"); return; }
        _names.Add(key.Name, (table, key));
        keys.Add(key);
        if (!_parents.TryGetValue(table, out var parents))
            _parents.Add(table, parents = new HashSet<string>(StringComparer.Ordinal));
        parents.Add(key.TargetTable);
    }

    private void CheckCycles()
    {
        var pending = _parents.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value, StringComparer.Ordinal));
        while (pending.Count > 0)
        {
            var ready = pending.Where(p => p.Value.Count == 0).Select(p => p.Key).ToArray();
            if (ready.Length == 0)
            {
                foreach (var entity in _entities.Where(e => pending.ContainsKey(e.Table)))
                    Error(entity, null, "DependencyCycle: " + string.Join(",", _keys
                        .Where(p => pending.ContainsKey(p.Key)).SelectMany(p => p.Value).Select(k => k.Name)));
                return;
            }
            foreach (var name in ready) pending.Remove(name);
            foreach (var parents in pending.Values) parents.ExceptWith(ready);
        }
    }

    private void Error(Entity entity, ForeignKeyIntent? intent, string reason)
    {
        _valid = false;
        _context.ReportDiagnostic(Diagnostic.Create(Invalid, intent?.Location ?? entity.Location ?? Location.None,
            ValidName(entity.Table) ? entity.Table : "<invalid>", intent?.Navigation ?? "<schema>", reason));
    }

    internal static bool ValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && !name.Any(char.IsControl) && Unicode(name);

    private static bool Unicode(string name)
    {
        for (var i = 0; i < name.Length; i++)
        {
            if (!char.IsSurrogate(name[i])) continue;
            if (!char.IsHighSurrogate(name[i]) || i + 1 == name.Length || !char.IsLowSurrogate(name[i + 1]))
                return false;
            i++;
        }
        return true;
    }
}
