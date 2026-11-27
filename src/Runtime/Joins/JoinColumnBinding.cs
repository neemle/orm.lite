using System.Data.Common;

namespace Neemle.OrmLite;

public sealed record JoinColumnBinding<T, TValue>(string Property, Func<DbDataReader, int, TValue> Read)
    where T : class;
