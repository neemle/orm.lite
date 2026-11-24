#nullable enable
using System;

namespace Neemle.OrmLite;

    public readonly struct JsonKey : IEquatable<JsonKey>
    {
        private readonly string? _name;

        public JsonKey(string key)
        {
            _name = key ?? throw new ArgumentNullException(nameof(key));
        }

        public string Name => _name ?? throw new InvalidOperationException(
            "this JsonKey was never constructed: it is `default` and carries no key.");

        public bool Equals(JsonKey other) => string.Equals(_name, other._name, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is JsonKey other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_name ?? "");

        public override string ToString() => _name ?? "";
    }
