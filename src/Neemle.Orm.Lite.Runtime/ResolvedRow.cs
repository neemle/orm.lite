#nullable enable
using System.Collections.Generic;

namespace Neemle.Orm.Lite;

    internal sealed class ResolvedRow
    {
        public ResolvedRow(IReadOnlyDictionary<string, object?> values, IReadOnlyList<string> clock)
        {
            Values = values;
            Clock = clock;
        }

        public IReadOnlyDictionary<string, object?> Values { get; }

        public IReadOnlyList<string> Clock { get; }
    }
