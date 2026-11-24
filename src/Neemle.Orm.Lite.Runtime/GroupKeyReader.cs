#nullable enable
using System;
using System.Data.Common;

namespace Neemle.Orm.Lite;

    // The row of a grouping that its key lambdas read. An enum column holds the member NAME as text, which the
    // driver's GetFieldValue<TEnum> does not read (Npgsql refuses it, Microsoft.Data.Sqlite turns the text into the
    // number 0), so an enum key goes through the same exact-name parse that a row mapper uses and an unknown name is
    // reported as an OrmEnumValueException. Every other key is read as before.
    internal readonly struct GroupKeyReader
    {
        private readonly DbDataReader _reader;
        private readonly string _table;

        internal GroupKeyReader(DbDataReader reader, string table)
        {
            _reader = reader;
            _table = table;
        }

        internal TValue Read<TValue>(int ordinal) =>
            IsEnum<TValue>() && !_reader.IsDBNull(ordinal)
                ? ScalarValue.To<TValue>(_reader.GetValue(ordinal), _table, _reader.GetName(ordinal))
                : GroupValue.Read<TValue>(_reader, ordinal);

        private static bool IsEnum<TValue>() => (Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue)).IsEnum;
    }
