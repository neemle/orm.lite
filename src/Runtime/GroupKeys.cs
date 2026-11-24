#nullable enable
using System.Linq;
using System.Linq.Expressions;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

    // The key columns of a grouping, and the first of them that is an enum column (null when none is), so that an
    // order by the keys can be refused by name: the database would order the stored member NAMES.
    internal sealed record GroupKeys(NodeList Nodes, string? EnumKey)
    {
        internal static GroupKeys Of(PredicateTranslator translator, params Expression[] keys) =>
            new(
                NodeList.Of(keys.Select(translator.ColumnOf)),
                keys.Select(EnumOrdering.KeyName).OfType<string>().FirstOrDefault());
    }
