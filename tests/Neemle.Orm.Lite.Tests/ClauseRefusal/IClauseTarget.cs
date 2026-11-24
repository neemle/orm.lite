namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

internal interface IClauseTarget
{
    Statement Compile(ClauseShape shape);

    string Render(ClauseShape shape);
}
