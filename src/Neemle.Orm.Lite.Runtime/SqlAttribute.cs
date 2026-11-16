namespace Neemle.Orm.Lite;

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class SqlAttribute : System.Attribute { public SqlAttribute(string sql) { } }
