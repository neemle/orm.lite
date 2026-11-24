#nullable enable
using System;

namespace Neemle.OrmLite;

    public readonly struct MovedFieldKey : IEquatable<MovedFieldKey>
    {
        private readonly string? _name;

        public MovedFieldKey(string name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (name.Length == 0)
                throw new ArgumentException(
                    "a moved field's key is the name of the column its value used to live in, and an "
                    + "empty one names nothing.", nameof(name));
            foreach (var c in name)
                if (c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_')
                    throw new ArgumentException(
                        $"the moved-field key `{name}` carries `{c}`, and this key is rendered into the "
                        + "statement as a SQL string literal so that an expression index over the same "
                        + "literal can be matched. Only the lowercase letters, digits and underscores a "
                        + "generated column name is made of are accepted: a name that needed an escape "
                        + "is a name OrmGenerator could not have emitted, so it is refused here rather "
                        + "than quietly quote-doubled.", nameof(name));
            _name = name;
        }

        public string Name => _name ?? throw new InvalidOperationException(
            "this MovedFieldKey was never constructed -- it is `default`, which carries no name. A "
            + "default key would render an empty literal and silently match no member of any document, "
            + "which is a wrong answer wearing the shape of a working one.");

        public bool Equals(MovedFieldKey other) => string.Equals(_name, other._name, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is MovedFieldKey other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_name ?? "");

        public override string ToString() => _name ?? "";
    }
