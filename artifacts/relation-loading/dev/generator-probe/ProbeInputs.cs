internal static class ProbeInputs
{
    private const string Limits = "[RelationLoadLimits(MaxDepth=8,MaxPaths=16,MaxNodes=32,MaxRoots=100,MaxRelatedRows=1000,MaxStatements=64)]";
    private const string Model = """
        using System;
        using System.Collections.Generic;
        using Neemle.OrmLite;
        namespace Probe;
        [Table("probe_authors",Managed=false)]
        LIMITS
        public class Author {
            [Key] public int Id { get; set; }
            public string? Name { get; set; }
            [RelationLoad(RelationLoadMode.Eager)] public List<Book> Books { get; set; } = new();
        }
        [Table("probe_books",Managed=false)]
        public class Book { [Key] public int Id { get; set; } public int AuthorId { get; set; }
            public Author? Author { get; set; } }
        """;

    internal static IEnumerable<ProbeInput> All()
    {
        var good = Model.Replace("LIMITS", Limits, StringComparison.Ordinal);
        yield return new("valid-six-named", good, false);
        yield return new("explicit-no-limits", Model.Replace("LIMITS", "").Replace("RelationLoadMode.Eager", "RelationLoadMode.Explicit"), false);
        var dimensions = new[] { "MaxDepth=8", "MaxPaths=16", "MaxNodes=32", "MaxRoots=100", "MaxRelatedRows=1000", "MaxStatements=64" };
        foreach (var dimension in dimensions)
        {
            var name = dimension.Split('=')[0];
            foreach (var value in new[] { "0", "-1" })
                yield return new(name + "-" + value, good.Replace(dimension, name + "=" + value), true);
            yield return new(name + "-missing", good.Replace(dimension + ",", "").Replace("," + dimension, ""), true);
        }
        yield return new("mode-invalid", good.Replace("RelationLoadMode.Eager", "(RelationLoadMode)42"), true);
        yield return new("mode-conflict", good.Replace("[RelationLoad(RelationLoadMode.Eager)]", "[RelationLoad(RelationLoadMode.Lazy)] [RelationLoad(RelationLoadMode.Eager)]"), true);
        yield return new("scalar-policy", good.Replace("public string? Name", "[RelationLoad(RelationLoadMode.Lazy)] public string? Name"), true);
        yield return new("keyless-source", good.Replace("[Key] public int Id", "public int Code"), true);
        yield return new("view-policy", good.Replace("[Table(\"probe_authors\",Managed=false)]", "[View(\"probe_authors\")]"), true);
        yield return new("escaped-member", good.Replace("Books", "@event"), false);
    }
}
