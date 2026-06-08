namespace Theragraf.IntegrationTests.Infrastructure;

/// <summary>
/// All classes decorated with [Collection(CosmosCollection.Name)] share the same
/// <see cref="CosmosFixture"/> instance.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CosmosCollection : ICollectionFixture<CosmosFixture>
{
    public const string Name = "CosmosEmulator";
}
