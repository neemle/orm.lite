#nullable enable
using System;

namespace Neemle.OrmLite;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotAColumnAttribute : Attribute
    {
    }
