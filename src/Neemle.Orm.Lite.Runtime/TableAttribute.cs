namespace Neemle.Orm.Lite;

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class TableAttribute : System.Attribute
    {
        public TableAttribute(string name) { }

        public bool Managed { get; set; } = true;
    }
