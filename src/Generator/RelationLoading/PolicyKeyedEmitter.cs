using System.Collections.Generic;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.PolicyCode;

namespace Neemle.OrmLite.Generator;

internal static class PolicyKeyedEmitter
{
    internal static IEnumerable<string> Members(Entity entity)
    {
        var key = OrmGenerator.FirstKey(entity)!;
        foreach (var bound in new[] { false, true })
        {
            var parameter = bound ? "global::System.Data.Common.DbConnection conn, " : "";
            var on = bound ? ".On(conn)" : "";
            yield return "public static " + Task + "<" + entity.Full + "?> GetAsync(" + Runtime
                + "OrmContext db, " + parameter + key.Cs + " key, " + Options + ") => "
                + Runtime + "RelationQueryExtensions.FirstOrDefaultAsync(Query(db)" + on + ".Where(e => e."
                + Identifier(key.Name) + " == key), options, ct);";
        }
    }
}
