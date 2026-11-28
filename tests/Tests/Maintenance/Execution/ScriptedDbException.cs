using System.Data.Common;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// A failure the scripted provider calls contention (or not), as a driver's busy error would be.
internal sealed class ScriptedDbException(string message, bool contention) : DbException(message)
{
    internal bool Contention { get; } = contention;
}
