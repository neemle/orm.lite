using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Prop = Neemle.OrmLite.OrmGenerator.Prop;
using TermDef = Neemle.OrmLite.OrmGenerator.TermDef;

namespace Neemle.OrmLite;

/// <summary>
/// Reads one term of an [Index]: a column or lower of a column, then optionally asc or desc, nulls first or nulls
/// last, and collate with a name. A term that cannot be read leaves a problem text and no term.
/// </summary>
internal static class IndexTerms
{
    internal const int NullsFirst = 1;

    internal const int NullsLast = 2;

    private static readonly Regex LowerTerm = new Regex(
        @"^lower\s*\(\s*(\w+)\s*\)\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>The ordering options of a term, apart from the column.</summary>
    internal readonly record struct TermOptions(bool Descending, int Nulls, string? Collation, bool Lower);

    /// <summary>The property of the entity that a name stands for, by property name first and then by column.</summary>
    internal static Prop? Resolve(List<Prop> props, string name, List<string> problems)
    {
        var found = props.FirstOrDefault(p => p.Name == name) ?? props.FirstOrDefault(p => p.Column == name);
        if (found is null) problems.Add("'" + name + "' is not a property or column of the entity");
        return found;
    }

    internal static TermDef Checked(Prop prop, TermOptions options, List<string> problems)
    {
        if ((options.Collation is not null || options.Lower) && prop.Read != "String")
            problems.Add("collate and lower apply to text columns, and '" + prop.Name + "' is " + prop.Cs);
        return new TermDef(prop.Column, options.Descending, options.Nulls, options.Collation, options.Lower);
    }

    internal static TermDef? Parse(string text, List<Prop> props, List<string> problems)
    {
        var (name, rest, wrapped) = Split(text.Trim());
        var ok = name.Length > 0;
        if (!ok) problems.Add("the term '" + text + "' names no column");
        var options = ReadModifiers(rest, text, problems);
        if (!ok || options is not { } read || Resolve(props, name, problems) is not { } prop) return null;
        return Checked(prop, read with { Lower = wrapped }, problems);
    }

    private static (string Name, string Remainder, bool Wrapped) Split(string trimmed)
    {
        var wrapped = LowerTerm.Match(trimmed);
        if (wrapped.Success) return (wrapped.Groups[1].Value, wrapped.Groups[2].Value, true);
        var cut = trimmed.IndexOfAny([' ', '\t']);
        return (cut < 0 ? trimmed : trimmed.Substring(0, cut), cut < 0 ? "" : trimmed.Substring(cut), false);
    }

    private static TermOptions? ReadModifiers(string rest, string text, List<string> problems)
    {
        var tokens = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var read = new Modifiers();
        for (var i = 0; i < tokens.Length; i++)
        {
            if (read.Take(tokens, ref i)) continue;
            problems.Add(
                "cannot read '" + tokens[i] + "' in the term '" + text + "'; a term is a column or lower of a column, "
                + "then optionally asc or desc, nulls first or nulls last, and collate with a name");
            return null;
        }
        return new TermOptions(read.Descending == true, read.Nulls, read.Collation, false);
    }

    /// <summary>The modifiers read so far from the words after the column of a term.</summary>
    private sealed class Modifiers
    {
        public bool? Descending { get; private set; }

        public int Nulls { get; private set; }

        public string? Collation { get; private set; }

        /// <summary>Takes the modifier that starts at the token and moves past it; false if none fits.</summary>
        public bool Take(string[] tokens, ref int i)
        {
            var word = tokens[i].ToLowerInvariant();
            return TakeDirection(word) || TakeNulls(tokens, word, ref i) || TakeCollation(tokens, word, ref i);
        }

        private bool TakeDirection(string word)
        {
            if (!(word == "asc" || word == "desc") || Descending is not null) return false;
            Descending = word == "desc";
            return true;
        }

        private bool TakeNulls(string[] tokens, string word, ref int i)
        {
            if (word != "nulls" || Nulls != 0 || i + 1 >= tokens.Length) return false;
            var side = tokens[i + 1].ToLowerInvariant();
            if (side is not ("first" or "last")) return false;
            Nulls = side == "first" ? NullsFirst : NullsLast;
            i++;
            return true;
        }

        private bool TakeCollation(string[] tokens, string word, ref int i)
        {
            if (word != "collate" || Collation is not null || i + 1 >= tokens.Length) return false;
            Collation = tokens[++i].Trim('"', '\'');
            return true;
        }
    }
}
