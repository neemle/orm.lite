using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed record StreamBorrowed(DbConnection Connection, DbTransaction? Transaction, UnitOfWork? UnitOfWork);
