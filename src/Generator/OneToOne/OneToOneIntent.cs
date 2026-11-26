using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

internal sealed record OneToOneIntent(string Navigation, string Target, string ForeignKey, string Inverse, bool Owner)
{
    internal string UniqueName { get; init; } = "";
    internal bool Assignable { get; init; }
    internal Location Location { get; init; } = Location.None;
}
