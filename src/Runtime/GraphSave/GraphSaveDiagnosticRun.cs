using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed record GraphSaveDiagnosticRun(DbCommand Command, StatementKind Kind, GatedDataSource.Lease Lease);
