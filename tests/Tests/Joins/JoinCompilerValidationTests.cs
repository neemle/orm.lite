using Xunit;
namespace Neemle.OrmLite.Tests.Joins;
[Collection("Graph")]
public sealed class JoinCompilerValidationTests
{
    private static JoinCompilerFixture<JoinCompilerValidationTests> Fixture(int capacity = 100) =>
        new(capacity: capacity);
    private static void Error(Action action, JoinError expected) =>
        JoinCompilerFixture<JoinCompilerValidationTests>.Error(action, expected);
    [Fact] public void Source_count_boundary_and_plus_one()
    {
        var f = Fixture(); var c = f.Query().IncludingDeleted().AsJoinSource("third");
        var on = f.B.Column(x => x.Id).Equal(c.Column(x => x.Id));
        var limits = new JoinLimits(2, 16, 128, 64, 10);
        _ = f.Build(f.Plan(limits: limits));
        Error(() => f.Plan(limits: limits).InnerJoin(c, on), JoinError.LimitExceeded);
    }
    [Fact] public void Projection_budget_includes_hidden_presence()
    {
        var f = Fixture(); _ = f.Build(f.Plan(limits: new(4, 3, 128, 64, 10)));
        Error(() => f.Build(f.Plan(limits: new(4, 2, 128, 64, 10))), JoinError.LimitExceeded);
    }
    [Fact] public void Combined_predicate_boundary_and_plus_one()
    {
        var f = Fixture(); var plan = f.Plan(limits: new(4, 16, 1, 64, 10)); _ = f.Build(plan);
        Error(() => f.Build(plan.Where(f.A.Column(x => x.Rank).GreaterThanValue(7))), JoinError.LimitExceeded);
    }
    [Fact] public void Request_parameter_boundary_and_plus_one()
    {
        var f = Fixture(); var plan = f.Plan(limits: new(4, 16, 128, 1, 10))
            .Where(f.A.Column(x => x.Rank).GreaterThanValue(7)); _ = f.Build(plan);
        Error(() => f.Build(plan.Where(f.B.Column(x => x.Rank).LessThanValue(11))), JoinError.ParameterCapacity);
    }
    [Fact] public void Provider_parameter_boundary_and_plus_one()
    {
        var f = Fixture(1); var plan = f.Plan().Where(f.A.Column(x => x.Rank).GreaterThanValue(7)); _ = f.Build(plan);
        Error(() => f.Build(plan.Where(f.B.Column(x => x.Rank).LessThanValue(11))), JoinError.ParameterCapacity);
    }
    [Fact] public void Requested_take_respects_materialization_bound()
    {
        var f = Fixture(); var plan = f.Plan().OrderBy(f.A.Column(x => x.Rank)); _ = f.Build(plan.Take(10));
        Error(() => f.Build(plan.Take(11)), JoinError.LimitExceeded);
    }
    [Fact] public void Take_zero_cannot_hide_foreign_projection()
    {
        var f = Fixture(); var foreign = f.Query().IncludingDeleted().AsJoinSource("foreign");
        var select = new JoinSelection(); select.Add(foreign.Column(x => x.Name), "foreignName");
        Error(() => f.Build(f.Plan().OrderBy(f.A.Column(x => x.Rank)).Take(0), select), JoinError.OutOfScopeSource);
        Assert.Equal(0, f.Probe.Connections);
    }
    [Fact] public void On_must_reference_new_right_and_an_admitted_left()
    {
        var f = Fixture(); var plan = JoinQueryPlan.From(f.A, JoinCompilerFixture<JoinCompilerValidationTests>.Limits);
        Error(() =>
        plan.InnerJoin(f.B, f.A.Column(x => x.Id).Equal(f.A.Column(x => x.Id))), JoinError.OutOfScopeSource);
        Error(() =>
        plan.InnerJoin(f.B, f.B.Column(x => x.Id).Equal(f.B.Column(x => x.Id))), JoinError.OutOfScopeSource);
    }
    [Fact] public void Foreign_result_column_cannot_substitute_matching_type()
    {
        var f = Fixture(); var foreign = f.Query().IncludingDeleted().AsJoinSource("foreign");
        Error(() =>
        f.Build(f.Plan().Where(foreign.Column(x => x.Rank).GreaterThanValue(1))), JoinError.OutOfScopeSource);
    }
    [Fact] public void Duplicate_exact_source_token_cannot_be_readded()
    {
        var f = Fixture(); Error(() => f.Plan().InnerJoin(f.B, f.On), JoinError.OutOfScopeSource);
    }
    [Fact] public void Compiled_plan_rechecks_current_metadata_identity()
    {
        var f = Fixture(); var plan = f.Build();
        OrmMeta.Register<JoinUnitRow<JoinCompilerValidationTests>>(JoinUnitModel<JoinCompilerValidationTests>.Create());
        try { Error(plan.ValidateCurrent, JoinError.UnsupportedSource); }
        finally
        {
            OrmMeta.Register<JoinUnitRow<JoinCompilerValidationTests>>(
                JoinUnitModel<JoinCompilerValidationTests>.Descriptor);
        }
    }
    [Fact] public void Bound_root_allows_same_context_unbound_right()
    {
        var f = Fixture(); var connection = new JoinExecutionConnection(f.Probe); connection.Open();
        var transaction = new JoinExecutionTransaction(connection, f.Probe);
        f.Configure(f.Query().IncludingDeleted().On(connection, transaction), f.Query().IncludingDeleted());
        _ = f.Build(); Assert.Equal(0, f.Probe.Commands);
    }
    [Fact] public void Unbound_root_refuses_bound_right()
    {
        var f = Fixture(); var connection = new JoinExecutionConnection(f.Probe); connection.Open();
        var transaction = new JoinExecutionTransaction(connection, f.Probe);
        f.Configure(f.Query().IncludingDeleted(), f.Query().IncludingDeleted().On(connection, transaction));
        Error(() => f.Build(), JoinError.ResourceMismatch);
    }
    [Fact] public void Identical_binding_accepts_but_foreign_context_refuses()
    {
        var f = Fixture(); var connection = new JoinExecutionConnection(f.Probe); connection.Open();
        var transaction = new JoinExecutionTransaction(connection, f.Probe);
        f.Configure(f.Query().IncludingDeleted().On(connection, transaction),
            f.Query().IncludingDeleted().On(connection, transaction)); _ = f.Build();
        var other = Fixture(); f.Configure(f.Query().IncludingDeleted(), other.Query().IncludingDeleted());
        Error(() => f.Build(), JoinError.ResourceMismatch);
    }
}
