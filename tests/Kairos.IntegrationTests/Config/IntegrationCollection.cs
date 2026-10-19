namespace Kairos.IntegrationTests.Config;

[CollectionDefinition("Integration Tests")]
public sealed class IntegrationCollection
    : ICollectionFixture<KairosApiFactory>
{
}