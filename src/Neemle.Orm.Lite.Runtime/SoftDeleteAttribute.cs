#nullable enable
using System;

namespace Neemle.Orm.Lite;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SoftDeleteAttribute(string column = "DeletedAt") : Attribute
    {
        public const string Platform = "<platform>";

        public string Column { get; } = column;

        public string? Tenant { get; init; }

        public string[] Unversioned { get; init; } = [];

        public string? Label { get; init; }

        public string[] Owners { get; init; } = [];

        public string[] Pinned { get; init; } = [];

        public string[] StatusColumns { get; init; } = [];
    }
