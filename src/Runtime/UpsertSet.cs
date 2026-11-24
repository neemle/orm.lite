#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;

namespace Neemle.OrmLite;

    public readonly struct UpsertSet
    {
        public string Column { get; }
        public int ParamIndex { get; }
        public long? IncrementBy { get; }
        public bool IsDatabaseClock { get; }
        public bool IsJsonDocument { get; }
        private UpsertSet(string column, int paramIndex, long? incrementBy, bool isDatabaseClock, bool isJsonDocument)
        { Column = column; ParamIndex = paramIndex; IncrementBy = incrementBy; IsDatabaseClock = isDatabaseClock; IsJsonDocument = isJsonDocument; }
        public static UpsertSet ToParam(string column, int paramIndex) => new UpsertSet(column, paramIndex, null, false, false);
        public static UpsertSet Increment(string column, long by) => new UpsertSet(column, -1, by, false, false);

        public static UpsertSet ToJsonDocumentParam(string column, int paramIndex) =>
            new UpsertSet(column, paramIndex, null, false, true);

        public static UpsertSet Now(string column) => new UpsertSet(column, -1, null, true, false);
    }
