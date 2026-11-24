using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using IndexDef = Neemle.OrmLite.Generator.OrmGenerator.IndexDef;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using TermDef = Neemle.OrmLite.Generator.OrmGenerator.TermDef;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Reads the [Index] declarations of an entity, on its properties and on the class, into index definitions. A
/// declaration that is not valid still becomes a definition, with its problems, so the diagnostic can name it.
/// </summary>
internal sealed class IndexReader
{
    internal const string IndexAttributeName = "Neemle.OrmLite.IndexAttribute";

    private readonly INamedTypeSymbol type;
    private readonly string table;
    private readonly List<Prop> props;

    private IndexReader(INamedTypeSymbol type, string table, List<Prop> props)
    {
        this.type = type;
        this.table = table;
        this.props = props;
    }

    internal static ImmutableArray<IndexDef> ReadIndexes(
        INamedTypeSymbol s, string table, List<Prop> props, List<(Prop Prop, AttributeData Attribute)> onProperties)
    {
        var reader = new IndexReader(s, table, props);
        var indexes = new List<IndexDef>();
        foreach (var (prop, a) in onProperties) indexes.Add(reader.Build(a, prop, []));
        foreach (var a in AttrsOf(s, IndexAttributeName))
            indexes.Add(reader.Build(a, null, IndexInputs.WrittenTerms(a)));
        return [.. indexes];
    }

    private IndexDef Build(AttributeData a, Prop? subject, List<string> written)
    {
        var problems = new List<string>();
        var terms = subject is not null ? PropertyTerms(a, subject, problems) : ClassTerms(a, written, problems);
        foreach (var group in terms.GroupBy(t => (t.Column, t.Lower)).Where(g => g.Count() > 1))
            problems.Add("column '" + group.Key.Column + "' appears more than once");
        var filter = IndexFilters.Parse(NamedString(a, "Where"), props, problems);
        var columns = terms.Select(t => t.Column).ToList();
        var explicitName = NamedString(a, "Name");
        var liveOnly = NamedBool(a, "LiveOnly");
        var name = explicitName ?? $"ix_{table}_{string.Join("_", columns)}{(liveOnly ? "_live" : "")}";
        return new IndexDef(
            name, [.. terms], NamedBool(a, "Unique"), liveOnly, explicitName is not null,
            AttributeLocation(a, type), filter, [.. problems]);
    }

    /// <summary>The one term of an [Index] on a property: the property itself, with the attribute's options.</summary>
    private static List<TermDef> PropertyTerms(AttributeData a, Prop subject, List<string> problems)
    {
        if (IndexInputs.WrittenTerms(a).Count > 0)
            problems.Add("an [Index] on a property takes no terms; list further columns in an [Index] on the class");
        var options = new IndexTerms.TermOptions(
            NamedBool(a, "Descending"), NamedInt(a, "Nulls"), NamedString(a, "Collation"), NamedBool(a, "Lower"));
        return [IndexTerms.Checked(subject, options, problems)];
    }

    /// <summary>The terms of an [Index] on the class: each one written as text, with its options in the text.</summary>
    private List<TermDef> ClassTerms(AttributeData a, List<string> written, List<string> problems)
    {
        var terms = new List<TermDef>();
        if (NamedBool(a, "Descending") || NamedBool(a, "Lower") || NamedInt(a, "Nulls") != 0
            || NamedString(a, "Collation") is not null)
            problems.Add(
                "Descending, Nulls, Collation and Lower belong to an [Index] on a property; in a class-level [Index] "
                + "write them in the term, for example \"Priority desc nulls last\"");
        if (written.Count == 0) problems.Add("it names no column");
        foreach (var text in written)
            if (IndexTerms.Parse(text, props, problems) is { } term) terms.Add(term);
        return terms;
    }
}
