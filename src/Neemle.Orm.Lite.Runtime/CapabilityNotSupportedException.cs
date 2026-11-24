namespace Neemle.Orm.Lite;

public sealed class CapabilityNotSupportedException : NotSupportedException
{
    public CapabilityNotSupportedException(string provider, string node, Capability capability, string? table = null, string? hint = null)
        : base(Describe(provider, node, capability, table, hint))
    {
        Provider = provider;
        Node = node;
        Capability = capability;
        Table = table;
        Hint = hint;
    }

    public string Provider { get; }

    public string Node { get; }

    public Capability Capability { get; }

    public string? Table { get; }

    public string? Hint { get; }

    private static string Describe(string provider, string node, Capability capability, string? table, string? hint) =>
        $"provider '{provider}' does not support {capability}, which {node} requires"
        + (table is null ? "" : $" (table '{table}')")
        + (hint is null ? "." : $". {hint}");
}
