namespace Neemle.OrmLite.Tests.Ports;

// The two kinds of token the neutrality scan looks at. A comment is neither: prose that names a provider is not
// coupling, a type, an id or a piece of SQL text is.
internal enum CouplingTokenKind
{
    Identifier,
    Literal,
}
