namespace Neemle.Orm.Lite;

    public interface IDdlDialect
    {
        ISqlProvider Provider { get; }
    }
