using System.Data.Common;

namespace Neemle.OrmLite;

public sealed record OneToOneReadScope(OrmContext Context, DbConnection Connection, DbTransaction? Transaction);
