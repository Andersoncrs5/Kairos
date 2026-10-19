namespace Kairos.IntegrationTests.Config;

[Collection("Integration Tests")]
public abstract class BaseIntegrationTest
{
    protected HttpClient Client { get; }

    protected KairosApiFactory Factory { get; }

    protected BaseIntegrationTest(KairosApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }
}