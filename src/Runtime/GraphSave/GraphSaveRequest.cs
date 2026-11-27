namespace Neemle.OrmLite;

public sealed record GraphSaveRequest<T>(T Root, GraphSaveIntent RootIntent, GraphSavePlan Plan) where T : class;
