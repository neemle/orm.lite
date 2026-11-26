using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

public sealed class LoadingScope : IAsyncDisposable
{
    private Task? _closing;
    internal object Gate { get; } = new();
    internal ScopeBinding Binding { get; }
    internal ScopeScheduler Scheduler { get; }
    internal ScopeAttachments Attachments { get; }
    internal ScopeClock Clock { get; }

    private LoadingScope(ScopeBinding binding)
    {
        Binding = binding;
        Scheduler = new ScopeScheduler(this);
        Attachments = new ScopeAttachments(this);
        Clock = new ScopeClock(binding.Options.Lifetime, () => _ = CloseAsync(RelationScopeError.Expired));
        try
        {
            binding.Context.PolicyLifetime.Add(this);
            binding.Unit?.PolicyLifetime.Add(this);
        }
        catch (Exception primary)
        {
            var cleanup = new PolicyCleanupSteps(primary);
            cleanup.Run(() => binding.Context.PolicyLifetime.Remove(this));
            cleanup.Run(Clock.Dispose);
            throw;
        }
        binding.Connection.StateChange += OnStateChange;
        Clock.Start();
    }

    public static async Task<LoadingScope> OpenAsync(OrmContext context, LoadingScopeOptions options,
        CancellationToken ct = default)
    {
        ValidateOptions(context, options);
        var connection = await context.DataSource.OpenConnectionAsync(ct);
        try
        {
            PolicyOrigins.Record(connection, context);
            return new LoadingScope(new ScopeBinding(context, connection, null, options) { OwnsConnection = true });
        }
        catch (Exception primary) { await PolicyCleanup.DisposeConnectionAsync(connection, primary); throw; }
    }

    public static LoadingScope Bind(OrmContext context, UnitOfWork unitOfWork, LoadingScopeOptions options)
    {
        ValidateOptions(context, options);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        if (!ReferenceEquals(unitOfWork.Provider, context.Provider))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
        var binding = new ScopeBinding(context, unitOfWork.Connection, unitOfWork.Transaction, options)
            { Unit = unitOfWork };
        PolicyOrigins.Require(binding.Connection, context, binding.Transaction);
        binding.Validate();
        return new LoadingScope(binding);
    }

    public static LoadingScope Bind(OrmContext context, DbConnection connection, DbTransaction? transaction,
        LoadingScopeOptions options)
    {
        ValidateOptions(context, options);
        ArgumentNullException.ThrowIfNull(connection);
        var unit = PolicyOrigins.Require(connection, context, transaction);
        var binding = new ScopeBinding(context, connection, transaction, options) { Unit = unit };
        binding.Validate();
        return new LoadingScope(binding);
    }

    public Task LoadAsync<T, TTarget>(T entity, GraphPath<T, TTarget> navigation, CancellationToken ct = default)
        where T : class where TTarget : class => Attachments.Demand(entity, navigation, false, ct);

    public Task RetryAsync<T, TTarget>(T entity, GraphPath<T, TTarget> navigation, CancellationToken ct = default)
        where T : class where TTarget : class => Attachments.Demand(entity, navigation, true, ct);

    public RelationLoadSnapshot State<T, TTarget>(T entity, GraphPath<T, TTarget> navigation)
        where T : class where TTarget : class => Attachments.State(entity, navigation);

    public void Detach<T>(T entity) where T : class => Attachments.Detach(entity);

    private static void ValidateOptions(OrmContext context, LoadingScopeOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (options?.Limits is null || options.MaxPendingDemands <= 0 || options.Lifetime <= TimeSpan.Zero)
            throw new RelationScopeException(RelationScopeError.InvalidOptions);
    }

    private void OnStateChange(object? sender, StateChangeEventArgs e)
    {
        if (e.CurrentState is ConnectionState.Closed or ConnectionState.Broken)
            _ = CloseAsync(RelationScopeError.InvalidResource);
    }

    internal Task CloseFromOwnerAsync() => CloseAsync(RelationScopeError.Closed);

    private Task CloseAsync(RelationScopeError error)
    {
        lock (Gate) return _closing ??= CloseCoreAsync(error);
    }

    private async Task CloseCoreAsync(RelationScopeError error)
    {
        var cleanup = new PolicyCleanupSteps();
        await cleanup.RunAsync(() => Scheduler.CloseAsync(error));
        cleanup.Run(Attachments.Invalidate);
        cleanup.Run(Clock.Dispose);
        cleanup.Run(() => Binding.Connection.StateChange -= OnStateChange);
        cleanup.Run(() => Binding.Context.PolicyLifetime.Remove(this));
        cleanup.Run(() => Binding.Unit?.PolicyLifetime.Remove(this));
        if (Binding.OwnsConnection)
            await cleanup.RunAsync(() => PolicyCleanup.DisposeConnectionAsync(Binding.Connection, null).AsTask());
        cleanup.ThrowIfFailed();
    }

    public ValueTask DisposeAsync() => new(CloseAsync(RelationScopeError.Closed));
}
