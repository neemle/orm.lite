from pathlib import Path
p=Path('src/Runtime/GraphPlanner.cs');s=p.read_text();s=s.replace('''        var state = query.GraphSnapshot();
        var registry''','''        return Build(query.GraphSnapshot(), request);
    }

    internal static GraphExecutionPlan<T> Build<T>(GraphQueryState<T> state, GraphRequest<T> request) where T : class
    {
        var registry''');pos=s.index('    private static void CheckRoot')
s=s[:pos]+'''    internal static GraphPlanNode PolicyNode<T>(GraphExecutionPlan<T> plan, GraphEntitySnapshot source,
        IGraphNavigationBinding edge) where T : class => AddNode(plan, [], source, edge, GraphModel.Snapshot());

'''+s[pos:];p.write_text(s)
p=Path('src/Runtime/GraphExecutor.cs');s=p.read_text();s=s.replace('''        var roots = await GraphStatementRunner.ReadAsync(context, plan.RootStatement,
            reader => GraphFrontier.Typed<T>(context.Identity.Intern(plan.Root.Binding, plan.Query.Map(reader))), true);''','''        var roots = await ReadRootsAsync(plan, context);''');pos=s.index('    private static async Task ExpandAsync')
s=s[:pos]+'''    internal static Task<List<T>> ReadRootsAsync<T>(GraphExecutionPlan<T> plan, GraphReadContext context)
        where T : class => GraphStatementRunner.ReadAsync(context, plan.RootStatement,
            reader => GraphFrontier.Typed<T>(context.Identity.Intern(plan.Root.Binding, plan.Query.Map(reader))), true);

'''+s[pos:];p.write_text(s)
p=Path('src/Runtime/GraphReadContext.cs');s=p.read_text().replace('    internal GraphIdentityMap Identity { get; } = new();','''    internal GraphIdentityMap Identity { get; } = new();
    internal Action<object, IGraphNavigationBinding, object?, Action>? Publication { get; init; }

    internal void Assign(object parent, IGraphNavigationBinding edge, object? value, Action publish)
    {
        if (Publication is { } stage) stage(parent, edge, value, publish);
        else publish();
    }''');p.write_text(s)
p=Path('src/Runtime/IGraphNavigationBinding.cs');s=p.read_text().replace('    bool Equivalent(', '    object? CaptureSourceKey(object source);\n    bool Equivalent(');p.write_text(s)
p=Path('src/Runtime/GraphNavigationBinding.cs');s=p.read_text().replace('    internal abstract bool Equivalent(', '    internal abstract object? CaptureSourceKey(TSource source);\n    internal abstract bool Equivalent(');s=s.replace('    bool IGraphNavigationBinding.Equivalent(','''    object? IGraphNavigationBinding.CaptureSourceKey(object source) =>
        CaptureSourceKey(GraphFrontier.Typed<TSource>(source));
    bool IGraphNavigationBinding.Equivalent(''');p.write_text(s)
p=Path('src/Runtime/GraphReferenceBinding.cs');s=p.read_text().replace('    internal override Type Target', '    internal override object CaptureSourceKey(TSource source) => _key(source);\n\n    internal override Type Target');old='''        foreach (var member in memberships)
            _assign(member.Parent, member.Key.HasValue && byKey.TryGetValue(member.Key.Value, out var target)
                ? target : null);''';new='''        foreach (var member in memberships)
        {
            var value = member.Key.HasValue && byKey.TryGetValue(member.Key.Value, out var target) ? target : null;
            context.Assign(member.Parent, this, value, () => _assign(member.Parent, value));
        }''';assert old in s;s=s.replace(old,new);p.write_text(s)
p=Path('src/Runtime/GraphCollectionBinding.cs');s=p.read_text().replace('    internal override Type Target', '    internal override object CaptureSourceKey(TSource source) => _sourceKey(source);\n\n    internal override Type Target');old='''        foreach (var source in sources)
            _assign(source, groups.TryGetValue(_sourceKey(source), out var group) ? new List<TTarget>(group) : []);''';new='''        foreach (var source in sources)
        {
            var value = groups.TryGetValue(_sourceKey(source), out var group) ? new List<TTarget>(group) : [];
            context.Assign(source, this, value, () => _assign(source, value));
        }''';assert old in s;s=s.replace(old,new);p.write_text(s)
p=Path('src/Runtime/GraphLinkBinding.cs');s=p.read_text().replace('    internal override Type Target', '    internal override object CaptureSourceKey(TSource source) => _sourceKey(source);\n\n    internal override Type Target');old='''        foreach (var source in sources)
            _assign(source, groups[_sourceKey(source)].Distinct().Where(k => targets.ContainsKey(k))
                .Select(k => targets[k]).ToList());''';new='''        foreach (var source in sources)
        {
            var value = groups[_sourceKey(source)].Distinct().Where(k => targets.ContainsKey(k))
                .Select(k => targets[k]).ToList();
            context.Assign(source, this, value, () => _assign(source, value));
        }''';assert old in s;s=s.replace(old,new);p.write_text(s)
