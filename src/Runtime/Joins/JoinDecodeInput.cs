using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed record JoinDecodeInput(DbDataReader Reader, int Ordinal, object Slot, bool Present, bool IsNull);
