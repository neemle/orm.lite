namespace Neemle.OrmLite.Tests.ClauseRefusal;

internal interface IClauseTarget
{
    Statement Compile(ClauseShape shape);

    string Render(ClauseShape shape);
}
