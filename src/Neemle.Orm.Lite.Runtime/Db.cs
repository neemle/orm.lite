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

namespace Neemle.Orm.Lite;

    public static class Db
    {
        public static bool Exists<T>(Expression<Func<T, bool>> predicate) =>
            throw new InvalidOperationException("Db.Exists is a query marker and cannot be executed directly.");

        public static DateTime Now() =>
            throw new InvalidOperationException("Db.Now is a query marker and cannot be executed directly.");

        public static bool AnyOf<TValue>(TValue column, IEnumerable<TValue> values) =>
            throw new InvalidOperationException("Db.AnyOf is a query marker and cannot be executed directly.");

        public static bool NullSafeEquals<TValue>(TValue column, TValue value) =>
            throw new InvalidOperationException("Db.NullSafeEquals is a query marker and cannot be executed directly.");

        public static bool EqualsIgnoreCase(string? column, string? value) =>
            throw new InvalidOperationException("Db.EqualsIgnoreCase is a query marker and cannot be executed directly.");

        public static bool JsonKeyEquals(Jsonb? column, string key, string value, JsonValueOrder order) =>
            throw new InvalidOperationException("Db.JsonKeyEquals is a query marker and cannot be executed directly.");

        public static bool JsonHasKey(Jsonb? column, string key) =>
            throw new InvalidOperationException("Db.JsonHasKey is a query marker and cannot be executed directly.");

        public static bool JsonArrayContainsString(Jsonb? column, string key, string value) =>
            throw new InvalidOperationException("Db.JsonArrayContainsString is a query marker and cannot be executed directly.");

        public static bool JsonArrayContainsAny(Jsonb? column, string key, IReadOnlyList<string> values) =>
            throw new InvalidOperationException("Db.JsonArrayContainsAny is a query marker and cannot be executed directly.");

        public static bool MovedFieldIsNull(Jsonb? document, MovedFieldKey key) =>
            throw new InvalidOperationException("Db.MovedFieldIsNull is a query marker and cannot be executed directly.");

        public static bool MovedFieldEquals<TValue>(Jsonb? document, MovedFieldKey key, TValue value) =>
            throw new InvalidOperationException("Db.MovedFieldEquals is a query marker and cannot be executed directly.");

        public static bool MovedFieldContains(Jsonb? document, MovedFieldKey key, string value) =>
            throw new InvalidOperationException("Db.MovedFieldContains is a query marker and cannot be executed directly.");
    }
