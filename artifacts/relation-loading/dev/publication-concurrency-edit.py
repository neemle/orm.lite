from pathlib import Path
p=Path('src/Runtime/RelationLoading/ScopeAttachments.cs');s=p.read_text();a=s.index('    internal Task Demand');b=s.index('    internal void Validate(object',a)
s=s[:a]+'''    internal Task Demand<T, TTarget>(T entity, GraphPath<T, TTarget> navigation, bool retry, CancellationToken ct)
        where T : class where TTarget : class
    {
        var edge = Edge(navigation);
        PolicyDemand demand;
        PolicyDemandWork work;
        lock (scope.Gate)
        {
            scope.Scheduler.Validate();
            var item = Get(entity);
            item.Validate(entity);
            if (item.Demands.TryGetValue(edge, out var existing) && !retry)
                return existing.Completion.Task.WaitAsync(ct);
            if (retry && (existing is null || existing.State is not
                (RelationLoadState.Faulted or RelationLoadState.Cancelled)))
                throw new RelationScopeException(RelationScopeError.InvalidOptions, edge.Navigation);
            demand = new PolicyDemand { State = RelationLoadState.Loading };
            item.Demands[edge] = demand;
            work = new PolicyDemandWork(entity, edge, item, item.Generation);
        }
        _ = RunDemandAsync(work, demand, ct);
        return demand.Completion.Task;
    }

    private async Task RunDemandAsync(PolicyDemandWork work, PolicyDemand demand, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, work.Attachment.Detached.Token);
        try
        {
            await scope.Scheduler.RunAsync(token => PolicyDemandRunner.RunAsync(scope, work, token), linked.Token);
            demand.Completion.TrySetResult();
        }
        catch (OperationCanceledException error)
        {
            lock (scope.Gate) demand.State = RelationLoadState.Cancelled;
            demand.Completion.TrySetCanceled(error.CancellationToken);
        }
        catch (Exception error)
        {
            lock (scope.Gate) demand.State = RelationLoadState.Faulted;
            demand.Completion.TrySetException(error);
        }
    }

    internal object? SourceKey(object entity, IGraphNavigationBinding edge)
    {
        lock (scope.Gate)
        {
            if (!_entities.TryGetValue(entity, out var item)) return edge.CaptureSourceKey(entity);
            item.Validate(entity);
            return item.SourceKey(edge);
        }
    }

    internal void ValidateKnown(object entity)
    {
        lock (scope.Gate)
            if (_entities.TryGetValue(entity, out var item)) item.Validate(entity);
    }

'''+s[b:];p.write_text(s)
p=Path('src/Runtime/RelationLoading/PolicyAttachment.cs');s=p.read_text().replace('    internal void Validate(object entity)','''    internal object? SourceKey(IGraphNavigationBinding edge) => _keys.TryGetValue(edge, out var value) ? value
        : throw new RelationScopeException(RelationScopeError.UnsupportedNavigation, edge.Navigation);

    internal void Validate(object entity)''');p.write_text(s)
p=Path('src/Runtime/RelationLoading/PolicyDemandRunner.cs');s=p.read_text().replace('''        using var registry = GraphModel.Enter();''','''        lock (scope.Gate) scope.Attachments.Validate(demand.Entity, demand.Generation);
        using var registry = GraphModel.Enter();''');p.write_text(s)
p=Path('src/Runtime/GraphReadContext.cs');s=p.read_text().replace('    internal Action<object, IGraphNavigationBinding, object?, Action>? Publication','    internal Func<object, IGraphNavigationBinding, object?>? SourceKeys { get; init; }\n    internal Action<object, IGraphNavigationBinding, object?, Action>? Publication');pos=s.rindex('}')
s=s[:pos]+'''    internal T SourceKey<T>(object parent, IGraphNavigationBinding edge, Func<T> current) =>
        SourceKeys is { } snapshot ? snapshot(parent, edge) is T key ? key
            : throw new GraphRequestException(GraphRequestError.InvalidBinding) : current();
'''+s[pos:];p.write_text(s)
p=Path('src/Runtime/RelationLoading/PolicyWork.cs');s=p.read_text().replace('{ Publication = Stage }','{ Publication = Stage, SourceKeys = scope.Attachments.SourceKey }');p.write_text(s)
p=Path('src/Runtime/GraphReferenceBinding.cs');s=p.read_text().replace('''var memberships = sources.Select(p => (Parent: p, Key: _key(p))).ToList();''','''var memberships = sources.Select(p =>
            (Parent: p, Key: context.SourceKey(p, this, () => _key(p)))).ToList();''');p.write_text(s)
for name in ['GraphCollectionBinding.cs','GraphLinkBinding.cs']:
 p=Path('src/Runtime')/name;s=p.read_text().replace('''var keys = sources.Select(_sourceKey).Distinct().Cast<object>().ToList();''','''var keys = sources.Select(p => context.SourceKey(p, this, () => _sourceKey(p)))
            .Distinct().Cast<object>().ToList();''').replace('groups.TryGetValue(_sourceKey(source),','groups.TryGetValue(context.SourceKey(source, this, () => _sourceKey(source)),').replace('groups[_sourceKey(source)]','groups[context.SourceKey(source, this, () => _sourceKey(source))]');p.write_text(s)
