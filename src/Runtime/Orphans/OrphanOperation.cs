namespace Neemle.OrmLite;

internal sealed class OrphanOperation
{
    internal OrphanOperation(OrmContext context, IOrphanCollection relation, object parentKey, OrphanOptions options)
    {
        if (context is null || relation is null || parentKey is null || options is null)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        Context = context;
        Relation = relation;
        ParentKey = parentKey;
        Options = options with { };
        Provider = context.Provider;
        Source = context.DataSource ?? throw OrphanRefusal.At(OrphanError.InvalidRequest);
        Request = new(relation.Parent.Metadata.Copy(), relation.Child.Metadata.Copy(), relation.Owner.Column,
            parentKey, options.Limits);
        if (parentKey.GetType() != relation.Parent.KeyType) throw OrphanRefusal.At(OrphanError.ParentMismatch);
        if (options.TransactionMode != OrphanTransactionMode.Owned)
            throw OrphanRefusal.At(OrphanError.UnsupportedTransaction);
        Validate();
    }

    internal OrmContext Context { get; }
    internal IOrphanCollection Relation { get; }
    internal object ParentKey { get; }
    internal OrphanOptions Options { get; }
    internal ISqlProvider Provider { get; }
    internal System.Data.Common.DbDataSource Source { get; }
    internal OrphanGuardRequest Request { get; }
    internal EntityDescriptor Child => Request.Child;

    internal void Validate()
    {
        if (Context.PolicyLifetime.IsClosed || !ReferenceEquals(Context.Provider, Provider)
            || !ReferenceEquals(Context.DataSource, Source))
            throw OrphanRefusal.At(OrphanError.ScopeEnded);
        if (Source is GatedDataSource gate && !ReferenceEquals(gate.Provider, Provider))
            throw OrphanRefusal.At(OrphanError.UnsupportedGuard);
        Relation.ValidateCurrent();
    }

    internal IOrphanSnapshotGuard EnabledGuard()
    {
        new OrphanGuardShape(Request).Mode(Options.Mode);
        return Provider as IOrphanSnapshotGuard ?? throw OrphanRefusal.At(OrphanError.UnsupportedGuard);
    }
}
