#nullable enable
using System;
using System.Linq.Expressions;

namespace Neemle.OrmLite;

    public sealed class UpsertSet<T>
    {
        private UpsertSet(Expression<Func<T, object?>> column, long? incrementBy, bool isNow)
        {
            Column = column ?? throw new ArgumentNullException(nameof(column));
            IncrementBy = incrementBy;
            IsNow = isNow;
        }

        internal Expression<Func<T, object?>> Column { get; }

        internal long? IncrementBy { get; }

        internal bool IsNow { get; }

        public static UpsertSet<T> Inserted(Expression<Func<T, object?>> column) => new(column, null, false);

        public static UpsertSet<T> Increment(Expression<Func<T, object?>> column, long by) => new(column, by, false);

        public static UpsertSet<T> Now(Expression<Func<T, object?>> column) => new(column, null, true);
    }
