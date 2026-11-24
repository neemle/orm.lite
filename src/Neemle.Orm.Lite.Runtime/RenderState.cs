#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    internal sealed class RenderState
    {
        private int _aliasCount;

        internal RenderState(ISqlProvider provider, string table, Func<string, string> col, List<object?> parameters)
        {
            Provider = provider;
            Table = table;
            Col = col;
            Params = parameters;
        }

        internal ISqlProvider Provider { get; }

        internal string Table { get; }

        internal Func<string, string> Col { get; }

        internal List<object?> Params { get; }

        // Redact and RedactWhen: the column (by its SQL name) to what the query returns in its place.
        internal Dictionary<string, Node> Redactions { get; } = new();

        internal int ParamBase { get; set; }

        internal bool Qualified { get; set; }

        internal Dictionary<ParameterExpression, (Node? Owner, Func<string, string> Col)> Scope { get; } = new();

        internal string NextAlias() => "s" + _aliasCount++;

        internal int AliasMark => _aliasCount;

        // Runs a translation that has pushed `parameters` into Scope. They leave the scope however it ends, and when
        // it throws the parameters it bound and the aliases it took (back to `aliasMark`) are given back, so the same
        // query renders and executes as if the failed call had not been made.
        internal T Scoped<T>(IReadOnlyList<ParameterExpression> parameters, int aliasMark, Func<T> translate)
        {
            var bound = Params.Count;
            try
            {
                return translate();
            }
            catch
            {
                _aliasCount = aliasMark;
                Params.RemoveRange(bound, Params.Count - bound);
                throw;
            }
            finally
            {
                foreach (var p in parameters) Scope.Remove(p);
            }
        }

        internal (int Aliases, int Parameters) Mark() => (_aliasCount, Params.Count);

        internal void Rewind((int Aliases, int Parameters) mark)
        {
            _aliasCount = mark.Aliases;
            Params.RemoveRange(mark.Parameters, Params.Count - mark.Parameters);
        }

        internal string ProviderName => Provider.Id;

        internal bool Allows(Capability capability) => Provider.Capabilities.Of(capability) != Support.None;

        internal int MaxBindParameters => Provider.MaxBindParameters;

        internal string Quote(string identifier) => Provider.CreateSerializer().Fragment(new ColumnRef(identifier));

        internal string Slot(int index) =>
            Provider.CreateSerializer().Fragment(new Param(ParamBase + index, null));

        internal Param Bind(object? value)
        {
            Params.Add(value);
            return new Param(ParamBase + Params.Count - 1, value);
        }

        internal Exception Refusal(Capability capability, string operation, string hint) =>
            new CapabilityNotSupportedException(Provider.Id, operation, capability, Table, hint);

        internal string Text(Node node) => Provider.CreateSerializer().Fragment(node);

        internal void RequireJsonDocument(string what)
        {
            if (!Allows(Capability.JsonDocument))
                throw Refusal(
                    Capability.JsonDocument, what,
                    $"{ProviderName} has no JSON document type, so it cannot {what}; a statement "
                    + "emitted anyway would carry PostgreSQL syntax to a server that cannot parse it.");
        }

        internal static string RequireKey(string key) =>
            key ?? throw new ArgumentNullException(
                nameof(key),
                "a JSON key must be a string; a null key binds SQL NULL and every row then fails to "
                + "match, which is an empty result rather than a refusal.");

        internal Node ValueOf(object? value, Param parameter)
        {
            if (value is not Jsonb) return parameter;
            RequireJsonDocument("bind a JSON document");
            return new Cast(parameter, CastType.Json);
        }

        internal Node BindValue(object? value)
        {
            if (value is Jsonb) RequireJsonDocument("bind a JSON document");
            return ValueOf(value, Bind(value));
        }

        internal Node? LiveTermOf(Type other, string alias, Func<string, string> col) =>
            OrmMeta.TryGetSoftDelete(other, out var soft)
                ? new IsNull(new ColumnRef(col(soft.Column), new AliasRef(alias)))
                : null;
    }
