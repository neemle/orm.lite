using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphRegistrationSafetyTests
{
    [Fact(DisplayName = "[AC-108-05] repeated reference/collection/link registrations retain one exact contract")]
    public void Equivalent_full_registration_is_idempotent_and_each_changed_contract_is_refused()
    {
        GraphDomain.Register();
        GraphModel.Register(SdetRegistrationBindings.Create());
        var original = GraphModel.Find(typeof(SdetRegistryRow));
        GraphModel.Register(SdetRegistrationBindings.Create());
        Assert.Same(original, GraphModel.Find(typeof(SdetRegistryRow)));
        for (var variant = 1; variant <= 11; variant++)
        {
            var error = Assert.Throws<GraphRequestException>(() =>
                GraphModel.Register(SdetRegistrationBindings.Create(variant)));
            Assert.Equal(GraphRequestError.ConflictingBinding, error.Error);
            Assert.Same(original, GraphModel.Find(typeof(SdetRegistryRow)));
        }
        Assert.Equal(3, original.Navigations.Count);
    }

    [Fact(DisplayName = "[SEC-108-01] an active graph rejects new registration and releases the lease afterwards")]
    public async Task In_call_registry_mutation_is_refused_without_poisoning_later_registration()
    {
        await using var db = await GraphDatabase.OpenAsync();
        GraphRequestException? refusal = null;
        GraphDomain.RootReadHook = () => refusal = Assert.Throws<GraphRequestException>(() =>
            GraphModel.Register(SdetRegistrationBindings.Late()));
        try
        {
            var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
            var roots = await db.Authors().ToGraphListAsync(request);
            Assert.Equal(3, roots.Count);
            Assert.Equal(GraphRequestError.ConflictingBinding, refusal!.Error);
            Assert.Equal(4, roots.Sum(root => root.Books.Count));
        }
        finally { GraphDomain.RootReadHook = null; }
        GraphModel.Register(SdetRegistrationBindings.Late());
        Assert.NotNull(GraphModel.Find(typeof(SdetLateRegistryRow)));
    }
}
