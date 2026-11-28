namespace Neemle.OrmLite;

/// <summary>An ANALYZE of the named scope.</summary>
/// <param name="Scope">The scope; required.</param>
public sealed record AnalyzeRequest(AnalyzeScope Scope)
{
    /// <summary>The table for <see cref="AnalyzeScope.Table"/>, by its generated descriptor; must be null for <see cref="AnalyzeScope.Database"/>.</summary>
    public EntityDescriptor? Table { get; init; }

    /// <summary>The whole budget of the call; null takes the provider's maintenance timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}
