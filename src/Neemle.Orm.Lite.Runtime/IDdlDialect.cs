namespace Neemle.Orm.Lite;

    public interface IDdlDialect
    {
        string Quote(string identifier);

        string Literal(object? value);
    }
