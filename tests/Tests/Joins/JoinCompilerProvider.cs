using Neemle.OrmLite.Nodes;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinCompilerProvider(ISqlProvider inner, int maximum = 100) : ISqlProvider
{
    internal ISerializer? Serializer;
    internal CapabilitySet? Declared;
    internal int Lowers;
    public string Id => inner.Id;
    public CapabilitySet Capabilities => Declared ?? inner.Capabilities;
    public int MaxBindParameters => maximum;
    public ISerializer CreateSerializer() => Serializer ?? inner.CreateSerializer();
    public Node Lower(Node tree) { Lowers++; return inner.Lower(tree); }
}
