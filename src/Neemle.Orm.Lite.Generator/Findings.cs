using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using MemberIssue = Neemle.Orm.Lite.OrmGenerator.MemberIssue;

namespace Neemle.Orm.Lite;

/// <summary>
/// What the reading of one entity found out about its members: the members the generated code cannot handle with the
/// reason, the members left out of the column set, and the keys that were lost that way.
/// </summary>
internal sealed class Findings
{
    public Findings(IAssemblySymbol? home) => Home = home;

    public IAssemblySymbol? Home { get; }

    public List<MemberIssue> Issues { get; } = new();

    public List<string> Excluded { get; } = new();

    public List<string> LostKeys { get; } = new();

    public bool Exclude(IPropertySymbol m, string? issue, bool key = false)
    {
        if (issue is null) return false;
        Add(m.Name, issue, SymbolFacts.LocationOf(m));
        if (key) LostKeys.Add(m.Name);
        return true;
    }

    public void Add(string member, string issue, Location location)
    {
        Issues.Add(new MemberIssue(member, issue, location));
        Excluded.Add(member);
    }
}
