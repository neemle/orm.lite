using System.Data.Common;

namespace Neemle.OrmLite.Tests.Graph.Security;

internal sealed record SecurityCommandRecord(string Text, IReadOnlyList<object?> Parameters,
    DbTransaction? Transaction);
