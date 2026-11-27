using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class QueryJoinSnapshot(JoinInput input, JoinEntityMetadata metadata,
    IReadOnlyDictionary<string, IJoinColumnBinding> columns, string alias, Select root)
{
    internal object Occurrence { get; } = new();
    internal string Alias { get; } = alias;
    internal JoinInput Input { get; } = input;
    internal JoinEntityMetadata Metadata { get; } = metadata;
    internal IReadOnlyDictionary<string, IJoinColumnBinding> Columns { get; } = columns;
    internal Select Root { get; } = root;

    internal void Current()
    {
        Metadata.Current();
        JoinResourceCapture.Validate(Input);
        if (Input.HasPolicy()) throw new JoinReadException(JoinError.UnsupportedVisibility);
        if (!Metadata.Binding.Maps(Input.Mapper)) throw new JoinReadException(JoinError.UnsupportedSource);
    }
}
