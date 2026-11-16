#nullable enable
using System;
using System.Collections.Generic;

namespace Neemle.Orm.Lite;

    public sealed class JsonKeyOrdering
    {
        public JsonKeyOrdering(
            JsonKey key, bool descending, bool nullsFirst, JsonValueOrder order, IReadOnlyList<string> arrangement)
        {
            Key = key;
            Descending = descending;
            NullsFirst = nullsFirst;
            Order = order;
            Arrangement = arrangement ?? throw new ArgumentNullException(nameof(arrangement));
        }

        public JsonKey Key { get; }

        public bool Descending { get; }

        public bool NullsFirst { get; }

        public JsonValueOrder Order { get; }

        public IReadOnlyList<string> Arrangement { get; }
    }
