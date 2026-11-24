using System.Text.RegularExpressions;

namespace Neemle.OrmLite.Tests.Ports;

// One way in which the provider-neutral sources can be coupled to a provider: a pattern over the identifier tokens or
// over the text of the string literals. Several markers may share an id; their tokens are counted together.
internal sealed class CouplingMarker(string id, CouplingTokenKind kind, string pattern)
{
    private readonly Regex _regex = new(pattern, RegexOptions.CultureInvariant);

    internal string Id { get; } = id;

    internal CouplingTokenKind Kind { get; } = kind;

    internal bool Matches(CouplingTokenKind tokenKind, string text) => tokenKind == Kind && _regex.IsMatch(text);
}
