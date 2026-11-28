namespace Neemle.OrmLite;

public static class Joined
{
    public static JoinQuery From<T>(JoinSource<T> root, JoinLimits limits) where T : class =>
        new(JoinQueryPlan.From(root, limits));
}
