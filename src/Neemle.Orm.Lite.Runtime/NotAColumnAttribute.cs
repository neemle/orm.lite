#nullable enable
using System;

namespace Neemle.Orm.Lite;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotAColumnAttribute : Attribute
    {
    }
