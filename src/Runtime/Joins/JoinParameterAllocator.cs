using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class JoinParameterAllocator(int capacity)
{
    private int _next;

    internal Param Bind(object? value)
    {
        if (_next >= capacity) throw new JoinReadException(JoinError.ParameterCapacity);
        return new Param(_next++, JoinScalarTypes.Value(value));
    }
}
