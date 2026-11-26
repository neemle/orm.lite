using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class StreamNativeLocks
{
    internal static void Validate(RowLock? rowLock, ISqlProvider provider)
    {
        if (rowLock is null) return;
        foreach (var capability in rowLock.Requires())
            if (provider.Capabilities.Of(capability) != Support.Native)
                throw new CapabilityNotSupportedException(provider.Id, rowLock.Name, capability,
                    hint: "Streaming row locks require native support.");
    }
}
