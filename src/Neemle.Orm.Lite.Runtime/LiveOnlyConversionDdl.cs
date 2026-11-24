#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public static class LiveOnlyConversionDdl
{
    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant);

    private static readonly Regex VersionName = new(@"\A[a-z0-9][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    private static readonly Regex KeyTerm = new(
        @"\A(?:(?<plain>[a-z][a-z0-9_]{0,62})|lower\((?<lowered>[a-z][a-z0-9_]{0,62})\))\z", RegexOptions.CultureInvariant);

    public static Node Up(
        string table, IReadOnlyList<string> keyTerms, string softColumn, string totalIndex, string liveIndex, bool liveIsDeclared, string version) =>
        Build(table, keyTerms, softColumn, totalIndex, liveIndex, liveIsDeclared, version, false);

    public static Node Down(
        string table, IReadOnlyList<string> keyTerms, string softColumn, string totalIndex, string liveIndex, bool liveIsDeclared, string version) =>
        Build(table, keyTerms, softColumn, totalIndex, liveIndex, liveIsDeclared, version, true);

    private static Node Build(
        string table, IReadOnlyList<string> keyTerms, string softColumn, string totalIndex, string liveIndex, bool liveIsDeclared, string version,
        bool revert)
    {
        var terms = Check(table, keyTerms, softColumn, totalIndex, liveIndex, version);
        return new ConvertToLiveOnlyIndex(table, NodeList.Of(terms), softColumn, totalIndex, liveIndex, liveIsDeclared, version, revert);
    }

    private static List<Node> Check(
        string table, IReadOnlyList<string> keyTerms, string softColumn, string totalIndex, string liveIndex, string version)
    {
        RequireIdentifier(table, nameof(table));
        RequireIdentifier(softColumn, nameof(softColumn));
        RequireIdentifier(totalIndex, nameof(totalIndex));
        RequireIdentifier(liveIndex, nameof(liveIndex));
        if (!VersionName.IsMatch(version))
            throw new ArgumentException("not a migration version: " + version, nameof(version));
        if (keyTerms is null || keyTerms.Count == 0)
            throw new ArgumentException("a conversion needs at least one key term", nameof(keyTerms));

        var terms = new List<Node>();
        foreach (var term in keyTerms)
        {
            var match = term is null ? Match.Empty : KeyTerm.Match(term);
            if (!match.Success)
                throw new ArgumentException("not a key term: " + term, nameof(keyTerms));
            terms.Add(match.Groups["plain"].Success
                ? new ColumnRef(match.Groups["plain"].Value)
                : new Call(Fn.Lower, [new ColumnRef(match.Groups["lowered"].Value)]));
        }
        return terms;
    }

    private static void RequireIdentifier(string value, string parameter)
    {
        if (!Identifier.IsMatch(value))
            throw new ArgumentException("not an identifier: " + value, parameter);
    }
}
