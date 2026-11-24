using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    public sealed class PgDdlDialect : IDdlDialect
    {
        public ISqlProvider Provider => ProviderRegistry.Require(ProviderRegistry.PostgresId);

        public string Quote(string identifier) => Provider.CreateSerializer().Fragment(new TableRef(identifier));

        public string Literal(object? value) => Provider.CreateSerializer().Fragment(DdlLiterals.Of(value));
    }
