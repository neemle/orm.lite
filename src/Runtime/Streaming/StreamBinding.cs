using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed record StreamBinding(DbDataSource DataSource, ISqlProvider Provider);
