#nullable enable
using System;
using System.Data.Common;

namespace Neemle.Orm.Lite
{
    public static class DbExtensions
    {
        public static DbParameter AddParam(this DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value switch
            {
                null => DBNull.Value,
                Enum e => e.ToString(),
                Jsonb j => j.Json,
                _ => value,
            };
            cmd.Parameters.Add(p);
            return p;
        }
    }
}
