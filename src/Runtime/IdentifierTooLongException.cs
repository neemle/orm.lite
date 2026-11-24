namespace Neemle.OrmLite;

// Raised when a migration operation declares a name the provider would cut: PostgreSQL keeps 63 bytes
// of an identifier and drops the rest without a word, so two long names can end up as one.
public sealed class IdentifierTooLongException : InvalidOperationException
{
    public IdentifierTooLongException(string provider, string kind, string name, int bytes, int limit)
        : base(Describe(provider, kind, name, bytes, limit))
    {
        Provider = provider;
        Kind = kind;
        Name = name;
        Bytes = bytes;
        Limit = limit;
    }

    public string Provider { get; }

    public string Kind { get; }

    public string Name { get; }

    public int Bytes { get; }

    public int Limit { get; }

    private static string Describe(string provider, string kind, string name, int bytes, int limit) =>
        $"the {kind} name '{name}' is {bytes} bytes in UTF-8 and provider '{provider}' keeps at most {limit}; "
        + "it would be cut there and could end up the same as another name. Give it a shorter name.";
}
