using System.Globalization;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class JoinCompilationContext(JoinQueryPlan plan)
{
    internal IReadOnlyList<QueryJoinSnapshot> Sources { get; } = plan.Sources;
    internal ISqlProvider Provider => plan.Root.Input.Database.Provider;
    internal JoinParameterAllocator Parameters { get; } = new(Math.Min(plan.Limits.MaxParameters,
        plan.Root.Input.Database.Provider.MaxBindParameters));

    internal int Index(QueryJoinSnapshot source)
    {
        for (var i = 0; i < Sources.Count; i++)
            if (ReferenceEquals(Sources[i], source)) return i;
        throw new JoinReadException(JoinError.OutOfScopeSource);
    }

    internal TableRef Table(QueryJoinSnapshot source) => new(source.Metadata.Table, Alias(source));
    internal string Alias(QueryJoinSnapshot source) => "j" + Index(source).ToString(CultureInfo.InvariantCulture);

    internal string OutputName(int ordinal)
    {
        var name = JoinAliases.InternalPrefix + ordinal.ToString(CultureInfo.InvariantCulture);
        if (Provider.Capabilities.MaxIdentifierBytes is { } limit && name.Length > limit)
            throw new JoinReadException(JoinError.LimitExceeded);
        return name;
    }

    internal Node Column(JoinColumnReference reference, bool projection)
    {
        _ = Index(reference.Source);
        if (!reference.Source.Columns.TryGetValue(reference.Binding.Property, out var found)
            || !ReferenceEquals(found, reference.Binding)) throw new JoinReadException(JoinError.UnsupportedSource);
        var column = new ColumnRef(reference.Source.Metadata.ProjectionSource(found),
            new AliasRef(Alias(reference.Source)));
        if (!found.Calculated) return column;
        if (!projection) throw new JoinReadException(JoinError.UnsupportedExpression);
        return new Call(Fn.Lower, [column]);
    }

    internal Node SourceColumn(ColumnRef column, QueryJoinSnapshot source)
    {
        var binding = JoinWhereNodes.Column(column, source.Metadata, source.Columns);
        return Column(new(source, binding), false);
    }
}
