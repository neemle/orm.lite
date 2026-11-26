using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;

namespace Neemle.OrmLite.Generator;

internal sealed class OneToOneAnalysis
{
    private static readonly DiagnosticDescriptor Invalid = new("ORMLITE036", "Invalid one-to-one declaration",
        "One-to-one navigation '{0}.{1}': {2}. The generated schema baseline is withheld.",
        "OrmLite", DiagnosticSeverity.Error, true);
    private readonly SourceProductionContext _context;
    private readonly ImmutableArray<Entity> _entities;
    private readonly ProviderSet _providers;
    private readonly List<OneToOneModel> _models = new();
    private readonly HashSet<string> _unique = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _inverse = new(StringComparer.Ordinal);

    internal OneToOneAnalysis(SourceProductionContext context, ImmutableArray<Entity> entities, ProviderSet providers)
    { _context = context; _entities = entities; _providers = providers; }

    internal bool Valid { get; private set; } = true;

    internal ImmutableArray<Entity> Resolve()
    {
        foreach (var owner in _entities)
            foreach (var intent in owner.OneToOneIntents.Where(i => i.Owner)) Resolve(owner, intent);
        CheckInverses();
        return _entities.Select(e => e with
        {
            OneToOnePairs = Valid ? _models.Where(m => m.OwnerFull == e.Full || m.PrincipalFull == e.Full)
                .ToImmutableArray() : default,
        }).ToImmutableArray();
    }

    private void Resolve(Entity owner, OneToOneIntent intent)
    {
        var target = _entities.FirstOrDefault(e => e.Full == intent.Target);
        if (target is null || !Supported(owner) || !Supported(target) || owner.Full == target.Full)
        { Error(owner, intent, "UnsupportedSchema"); return; }
        if (!intent.Assignable) { Error(owner, intent, "InvalidNullability"); return; }
        var inverse = target.OneToOneIntents.FirstOrDefault(i => i.Navigation == intent.Inverse);
        if (inverse is null || inverse.Owner || inverse.UniqueName.Length != 0
            || inverse.Target != owner.Full || inverse.ForeignKey != intent.Navigation)
        { Error(owner, intent, "InvalidInverse"); return; }
        if (!inverse.Assignable) { Error(owner, intent, "InvalidNullability"); return; }
        var primary = Key(target);
        var local = owner.Props.FirstOrDefault(p => p.Name == intent.ForeignKey);
        if (primary is null || Key(owner) is null) { Error(owner, intent, "UnsupportedKey"); return; }
        if (local is null) { Error(owner, intent, "UnmappedForeignKey"); return; }
        if (ForeignKeyAnalysis.Shape(local) != ForeignKeyAnalysis.Shape(primary))
        { Error(owner, intent, "IncompatibleKey"); return; }
        var constraint = Constraint(owner, intent);
        if (constraint is null || !Names(owner, intent)) return;
        Add(owner, target, intent, (local, primary), constraint);
    }

    private void Add(Entity owner, Entity target, OneToOneIntent intent, (Prop Local, Prop Key) columns,
        ForeignKeyIntent constraint)
    {
        if (_models.Any(m => m.OwnerFull == owner.Full && m.ForeignKeyColumn == columns.Local.Column)
            || !_inverse.Add(target.Full + "\0" + intent.Inverse))
        { Error(owner, intent, "AmbiguousPair"); return; }
        _models.Add(new OneToOneModel(intent, owner.Full, target.Full, columns.Local.Column, columns.Key.Column)
        {
            OwnerToken = owner.Type, PrincipalToken = target.Type, KeyType = columns.Key.Cs,
            PrincipalKeyProperty = columns.Key.Name, Nullable = columns.Local.Nullable,
            NullableValue = columns.Local.NullableValue, ForeignKeyName = constraint.Name, Action = constraint.Action,
        });
    }

    private ForeignKeyIntent? Constraint(Entity owner, OneToOneIntent intent)
    {
        var keys = owner.ForeignKeyIntents.Where(i => i.Navigation == intent.Navigation && i.End is null).ToArray();
        if (keys.Length == 1 && intent.UniqueName.Length != 0) return keys[0];
        Error(owner, intent, "MissingConstraintIntent");
        return null;
    }

    private bool Names(Entity owner, OneToOneIntent intent)
    {
        if (!ForeignKeyAnalysis.ValidName(intent.UniqueName) || !_unique.Add(intent.UniqueName)
            || _entities.SelectMany(e => e.Indexes).Any(i => string.Equals(i.Name, intent.UniqueName,
                StringComparison.OrdinalIgnoreCase))
            || _entities.SelectMany(e => e.ForeignKeyIntents).Any(k => string.Equals(k.Name, intent.UniqueName,
                StringComparison.OrdinalIgnoreCase)))
        { Error(owner, intent, "ConflictingConstraint"); return false; }
        if (_providers.Providers.Any(p => _providers.Find(p)?.MaxIdentifierBytes is { } limit
            && Encoding.UTF8.GetByteCount(intent.UniqueName) > limit))
        { Error(owner, intent, "ConflictingConstraint: identifier exceeds provider byte limit"); return false; }
        return true;
    }

    private void CheckInverses()
    {
        foreach (var entity in _entities)
            foreach (var inverse in entity.OneToOneIntents.Where(i => !i.Owner))
                if (!_models.Any(m => m.PrincipalFull == entity.Full && m.Intent.Inverse == inverse.Navigation))
                    Error(entity, inverse, "InvalidInverse");
    }

    private static bool Supported(Entity e) => e.Managed && !e.IsView && !OrmGenerator.Dropped(e);
    private static Prop? Key(Entity e) => e.Props.Count(p => p.IsKey) == 1
        && e.LostKeys.IsEmpty ? e.Props.FirstOrDefault(p => p.IsKey && !p.Nullable) : null;

    private void Error(Entity owner, OneToOneIntent intent, string reason)
    {
        Valid = false;
        _context.ReportDiagnostic(Diagnostic.Create(Invalid, intent.Location, owner.Type, intent.Navigation, reason));
    }
}
