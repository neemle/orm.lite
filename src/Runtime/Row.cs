#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Neemle.OrmLite;

    public sealed class Row<T>
    {
        internal List<(Expression<Func<T, object?>> Column, object? Value)> Values { get; } = new();

        internal List<Expression<Func<T, object?>>> Clock { get; } = new();

        public Row<T> With(Expression<Func<T, object?>> column, object? value)
        {
            Values.Add((column ?? throw new ArgumentNullException(nameof(column)), value));
            return this;
        }

        public Row<T> WithNow(Expression<Func<T, object?>> column)
        {
            Clock.Add(column ?? throw new ArgumentNullException(nameof(column)));
            return this;
        }
    }
