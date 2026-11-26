using System.Security.Cryptography;
using System.Text;

namespace Neemle.OrmLite;

internal sealed class SchemaDiffCanonical
{
    private readonly StringBuilder _text = new();

    internal void Add(string? text)
    {
        if (text is null) { _text.Append("-1:"); return; }
        _text.Append(text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(text);
    }

    internal void Fact<T>(SchemaFact<T> fact, Action<T> known)
    {
        Add(fact.Knowledge.ToString());
        if (fact.Knowledge == SchemaKnowledge.Known) known(fact.GetKnown());
        else Add(fact.Reason);
    }

    internal string Digest() => Hash(_text.ToString());

    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
