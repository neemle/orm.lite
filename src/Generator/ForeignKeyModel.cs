namespace Neemle.OrmLite.Generator;

internal sealed record ForeignKeyModel(string Name, string Column, string TargetTable, string TargetColumn, int Action);
