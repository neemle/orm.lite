using System.Data.Common;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinExecutionProbe
{
    internal string[] Headers = ["__orm_join_0", "__orm_join_1", "__orm_join_2"];
    internal object?[][] Rows = [["root", "note", Guid.Parse("00000000-0000-0000-0000-000000000001")]];
    internal int Opens, Connections, Commands, Executes, Reads, Decodes, GuidDecodes;
    internal int ReaderDisposals, CommandDisposals, ConnectionDisposals, Commits, Rollbacks, TxDisposals;
    internal int ReadFaultAt, DecodeFaultAt;
    internal Exception? OpenError, ExecuteError, ReadError, DecodeError, ReaderError, CommandError, ConnectionError;
    internal CancellationToken OpenToken, ExecuteToken, ReadToken;
    internal Func<CancellationToken, Task>? Opening, Executing, Reading;
    internal Action<bool>? AfterRead;
    internal readonly List<string> Events = [];
    internal readonly List<DbCommand> Issued = [];
    internal void Event(string name) { lock (Events) Events.Add(name); }
}
