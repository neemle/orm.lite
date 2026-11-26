#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed class OrmSchema
{
    public const string BaselinePrefix = "0000_baseline_";

    public const string IndexesPrefix = "0001_orm_indexes_";

    public const string ReconcilePrefix = "9900_orm_index_reconcile_";

    private OrmSchema(string token, IReadOnlyList<EntityDescriptor> entities)
    {
        Token = token;
        Entities = entities;
    }

    public string Token { get; }

    public IReadOnlyList<EntityDescriptor> Entities { get; }

    public bool HasSoftDeletion => Entities.Any(e => e.Soft is not null);

    public static OrmSchema Of(string token, params EntityDescriptor[] entities)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(entities);
        return new OrmSchema(token, entities);
    }

    public IReadOnlyList<RowHistoryTable> HistoryTables =>
        Entities.OrderBy(e => e.Table, StringComparer.Ordinal).Select(e => e.HistoryTable()).OfType<RowHistoryTable>().ToList();

    public IReadOnlyList<Node> Baseline()
    {
        if (ForeignKeyBaseline.HasIntent(Entities)) return ForeignKeyBaseline.Create(Entities);
        var plan = new List<Node>();
        foreach (var entity in Entities)
        {
            plan.AddRange(EntityStatements.CreateTables(entity));
            plan.AddRange(EntityStatements.CreateJoinTables(entity));
        }
        return plan;
    }

    public IReadOnlyList<Node> Indexes() => Entities.SelectMany(EntityStatements.CreateIndexes).ToList<Node>();

    public Node Reconcile() => new ReconcileIndexes(NodeList.Of(Entities.SelectMany(EntityStatements.ReconcileIndexes)));

    // The identity of the declared index set: a hash of the declarations themselves (table, name, uniqueness, live
    // column, columns and shape), never of rendered text. It names the reconcile version, so it is the same whichever
    // providers are loaded; a database that carries the name built from rendered text runs the renamed reconcile once,
    // which is idempotent, and records the new version beside the old one.
    public string IndexSetHash => StableHash(IdentityOfIndexes());

    public MigrationContribution[] Contributions(params MigrationContribution[] migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        var all = new List<MigrationContribution>
        {
            MigrationContribution.Of(BaselinePrefix + Token, Reversibility.Irreversible, Baseline()),
            MigrationContribution.Of(IndexesPrefix + Token, Reversibility.Reversible, Indexes()),
        };
        all.AddRange(migrations.OrderBy(m => m.Version, StringComparer.Ordinal));
        all.Add(MigrationContribution.Of(
            ReconcilePrefix + Token + "_" + IndexSetHash, Reversibility.Irreversible, [Reconcile()]));
        if (HasSoftDeletion)
        {
            var tables = HistoryTables;
            all.Add(new MigrationContribution(
                RowHistoryDdl.VersionPrefix + Token + "_" + RowHistoryDdl.SetHash(tables),
                Reversibility.Irreversible,
                provider => DdlCompiler.Render([RowHistoryDdl.Install(tables)], provider),
                null)
            {
                Nodes = _ => [RowHistoryDdl.Install(tables)],
            });
        }
        return all.ToArray();
    }

    private IEnumerable<string> IdentityOfIndexes() =>
        Entities.Where(e => e.Managed).SelectMany(e => e.Indexes.Select(i => IdentityOf(e, i)));

    private static string IdentityOf(EntityDescriptor entity, IndexDescriptor index) =>
        string.Join(
            "|",
            entity.Table,
            index.Name,
            index.Unique ? "unique" : "plain",
            index.LiveOnly && entity.Soft is not null ? entity.Soft.Column : "",
            string.Join(",", index.Columns)) + index.Shape;

    public static string StableHash(IEnumerable<string> statements)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL, prime = 1099511628211UL;
            var h = offset;
            foreach (var s in statements.OrderBy(x => x, StringComparer.Ordinal))
            {
                foreach (var ch in s) { h ^= ch; h *= prime; }
                h ^= '\n'; h *= prime;
            }
            return h.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
