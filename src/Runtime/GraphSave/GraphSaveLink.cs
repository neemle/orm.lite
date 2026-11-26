namespace Neemle.OrmLite;

internal sealed record GraphSaveLink(string Table, string FirstColumn, Guid FirstKey, string SecondColumn, Guid SecondKey);
