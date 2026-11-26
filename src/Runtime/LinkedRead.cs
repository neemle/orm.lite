#nullable enable
using System;
using System.Data.Common;

namespace Neemle.OrmLite;

public sealed record LinkedRead<T>(
    EntityDescriptor Owner,
    string Navigation,
    object? Key,
    EntityDescriptor Target,
    Func<DbDataReader, T> Map);
