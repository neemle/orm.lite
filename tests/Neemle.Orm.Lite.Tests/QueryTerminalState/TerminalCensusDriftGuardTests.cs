using System.Reflection;
using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public sealed class TerminalCensusDriftGuardTests
{
    private static readonly Dictionary<string, string> Excluded = new()
    {
        ["Bind"] = "it writes the command it is given and reads the query's bound parameters, never the query",
        ["AsView"] = "it needs a view entity this census does not register and is not a statement terminal; "
            + "it leaves the alias counter and the bound parameters of the query as they were (ORML-75, pinned in "
            + "Views/AsViewSideEffectTests)",
    };

    private static List<string> Terminals() => typeof(Query<BehaveNote>)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName && !IsBuilder(m.ReturnType))
        .Select(m => m.Name)
        .Distinct()
        .Order()
        .ToList();

    private static List<string> PublicNames() => typeof(Query<BehaveNote>)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName)
        .Select(m => m.Name)
        .Distinct()
        .Order()
        .ToList();

    private static bool IsBuilder(Type type) =>
        type == typeof(Query<BehaveNote>)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Grouping<,>));

    private static List<string> Covered() =>
        TerminalCensus.Entries.SelectMany(e => e.Members).Distinct().Order().ToList();

    [Fact(DisplayName = "AC7: every public instance method of Query<T> that does not return a builder is in the " +
        "census or in a named exclusion, so a new terminal that is not covered fails")]
    public void Every_terminal_is_covered_or_justified()
    {
        var missing = Terminals().Where(n => !Covered().Contains(n) && !Excluded.ContainsKey(n)).ToList();

        Assert.True(missing.Count == 0,
            "Query<T> has terminals the no-write census does not run; "
            + "add them to TerminalCensus.Entries or justify the exclusion:\n"
            + string.Join("\n", missing));
    }

    [Fact(DisplayName = "AC7: the census names only real terminals and no exclusion is stale or also covered, so the " +
        "guard cannot pass on typos")]
    public void The_census_and_the_exclusions_name_real_terminals()
    {
        var terminals = Terminals();
        var names = PublicNames();

        Assert.NotEmpty(Covered());
        Assert.All(Covered(), name => Assert.Contains(name, names));
        Assert.All(Excluded.Keys, name => Assert.Contains(name, terminals));
        Assert.All(Excluded.Keys, name => Assert.DoesNotContain(name, Covered()));
        Assert.All(Excluded.Values, reason => Assert.NotEmpty(reason));
    }
}
