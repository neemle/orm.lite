using System.Linq.Expressions;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.Corpus;

public sealed class CorpusRun
{
    public CorpusRun(CorpusRecorder recorder, string root)
    {
        Rec = recorder;
        Root = root;
        Pg = new OrmContext(new NeverUsedDataSource(), PostgresProvider.Instance);
        NoCap = new OrmContext(new NeverUsedDataSource(), new NoCapabilityProvider());
    }

    public CorpusRecorder Rec { get; }

    public string Root { get; }

    public OrmContext Pg { get; }

    public OrmContext NoCap { get; }

    public Query<T> Q<T>() where T : class => CorpusWorld.Of<T>(Pg);

    public Query<T> QNoCap<T>() where T : class => CorpusWorld.Of<T>(NoCap);

    public void Select<T>(string group, string name, Func<Query<T>, Query<T>> build) where T : class =>
        Rec.Statement(group, name, () =>
        {
            var q = build(Q<T>());
            return Read(q, q.CompileSelect(), q.RenderSelect());
        });

    public void Update<T>(string group, string name, Func<Query<T>, Query<T>> build) where T : class =>
        Rec.Statement(group, name, () =>
        {
            var q = build(Q<T>());
            return Read(q, q.CompileUpdate(), q.RenderUpdate());
        });

    public void Delete<T>(string group, string name, Func<Query<T>, Query<T>> build) where T : class =>
        Rec.Statement(group, name, () =>
        {
            var q = build(Q<T>());
            return Read(q, q.CompileDelete(), q.RenderDelete());
        });

    public void Purge<T>(string group, string name, Func<Query<T>, Query<T>> build) where T : class =>
        Rec.Statement(group, name, () =>
        {
            var q = build(Q<T>());
            return Read(q, q.CompilePurge(), q.RenderPurge());
        });

    public void Aggregates<T>(string group, string name, Func<Query<T>, Query<T>> build) where T : class =>
        Rec.Statement(group, name, () =>
        {
            var q = build(Q<T>());
            return Read(q, q.CompileAggregates(), q.RenderAggregates());
        });

    public void Projection<T>(string group, string name, Func<Query<T>, Query<T>> build) where T : class =>
        Rec.Statement(group, name, () =>
        {
            var q = build(Q<T>());
            return Read(q, q.CompileProjection(), q.RenderProjection());
        });

    public void Project<T>(string group, string name, Func<Query<T>, Query<T>> build, params Expression<Func<T, object?>>[] columns) where T : class =>
        Rec.Statement(group, name, () =>
        {
            var q = build(Q<T>());
            return Read(q, q.CompileProject(columns), q.RenderProject(columns));
        });

    public static (string Sql, IReadOnlyList<object?> Parameters) Read<T>(Query<T> q, Statement compiled, string rendered) =>
        Agree(compiled, rendered, q.BoundParameters, allowUnbound: false);

    public static (string Sql, IReadOnlyList<object?> Parameters) Keyed<T>(Query<T> q, Statement compiled, string rendered) =>
        Agree(compiled, rendered, q.BoundParameters, allowUnbound: true);

    public static (string Sql, IReadOnlyList<object?> Parameters) Listed(Statement compiled, string rendered, IReadOnlyList<object?> bound) =>
        Agree(compiled, rendered, bound, allowUnbound: false);

    private static (string Sql, IReadOnlyList<object?> Parameters) Agree(Statement compiled, string rendered, IReadOnlyList<object?> bound, bool allowUnbound)
    {
        if (!string.Equals(compiled.Text, rendered, StringComparison.Ordinal))
            throw new InvalidOperationException("the compiled statement and the rendered text differ: " + compiled.Text + " | " + rendered);
        foreach (var parameter in compiled.Parameters)
        {
            if (parameter.Ordinal >= bound.Count)
            {
                if (allowUnbound) continue;
                throw new InvalidOperationException("the compiled parameter " + parameter.Name + " is beyond the bound list");
            }
            if (!Equals(parameter.Value, bound[parameter.Ordinal]))
                throw new InvalidOperationException("the compiled parameter " + parameter.Name + " differs from the bound list");
        }
        return (compiled.Text, bound.ToList());
    }

    public void SelectRefusal<T>(string group, string name, Action<Query<T>> act) where T : class =>
        Rec.Refusal(group, name, () => act(Q<T>()));
}

public static class CorpusExtensions
{
    public static Query<T> Query<T>(this OrmContext ctx) where T : class => CorpusWorld.Of<T>(ctx);
}
