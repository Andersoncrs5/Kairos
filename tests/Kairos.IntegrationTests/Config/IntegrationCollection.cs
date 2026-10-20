using Xunit;

namespace Kairos.IntegrationTests.Config;

[CollectionDefinition("Integration Tests")]
public sealed class IntegrationTestCollection
    : ICollectionFixture<KairosApiFactory>
{
}