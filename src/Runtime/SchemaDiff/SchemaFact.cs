namespace Neemle.OrmLite;

public sealed class SchemaFact<T>
{
    private readonly T _value;

    private SchemaFact(SchemaKnowledge knowledge, T value, string? reason)
    {
        Knowledge = knowledge;
        _value = value;
        Reason = reason;
    }

    public SchemaKnowledge Knowledge { get; }
    public string? Reason { get; }

    public T GetKnown() => Knowledge == SchemaKnowledge.Known
        ? _value : throw new SchemaDiffException(SchemaDiffError.UnknownMetadata);

    public static SchemaFact<T> Known(T value) => new(SchemaKnowledge.Known, value, null);

    public static SchemaFact<T> Unknown(string reason) =>
        new(SchemaKnowledge.Unknown, default!, SchemaDiffNames.Required(reason));

    public static SchemaFact<T> Unsupported(string reason) =>
        new(SchemaKnowledge.Unsupported, default!, SchemaDiffNames.Required(reason));

    internal SchemaFact<T> Copy(Func<T, T> copy) => Knowledge switch
    {
        SchemaKnowledge.Known => Known(copy(_value)),
        SchemaKnowledge.Unknown => Unknown(Reason!),
        SchemaKnowledge.Unsupported => Unsupported(Reason!),
        _ => throw new SchemaDiffException(SchemaDiffError.InvalidRequest),
    };
}
