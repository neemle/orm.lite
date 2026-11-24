#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Neemle.OrmLite;

    // The values of a Contains over a column, checked before one of them is bound. Contains expands to one placeholder
    // per value, so a list longer than the bind limit of the provider used to fail inside the driver; and a string
    // asked to contain a string column is a substring test, which an IN list of its characters does not express.
    internal static class InValues
    {
        internal static List<object?> Of(RenderState state, object? collection, Type column)
        {
            if (collection is not IEnumerable values)
                throw new NotSupportedException("IN requires an evaluable collection.");
            if (collection is string && (Nullable.GetUnderlyingType(column) ?? column) == typeof(string))
                throw new NotSupportedException(
                    "Cannot test a string column with Contains over a string value: that is a substring test of the "
                    + "value, and the translation would test the column against each character of it. Nothing was "
                    + "executed. Pass the values to compare with as an array or a list.");
            var list = values.Cast<object?>().ToList();
            var slots = state.ParamBase + state.Params.Count + list.Count;
            if (slots > state.MaxBindParameters)
                throw new NotSupportedException(
                    $"Contains over {list.Count} values needs {slots} bind parameters in this statement and "
                    + $"{state.ProviderName} binds at most {state.MaxBindParameters} in one. Nothing was executed. "
                    + "Split the list into smaller batches and run one statement for each.");
            return list;
        }
    }
