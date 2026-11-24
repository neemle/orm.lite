namespace Neemle.Orm.Lite;

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class NumericAttribute : System.Attribute { public NumericAttribute(int precision, int scale) { } }
