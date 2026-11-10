using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kairos.IntegrationTests.Config;

[Collection("Integration Tests")]
public sealed class RateLimitingTests(KairosApiFactory factory)
{
    [Fact]
    public async Task Login_ExceedingLimit_Returns429_WithRetryAfter()
    {
        using var limited = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Enabled"] = "true",
                    ["RateLimiting:AuthLogin:PermitLimit"] = "2",
                    ["RateLimiting:AuthLogin:WindowSeconds"] = "60"
                })));

        using var client = limited.CreateClient();

        HttpResponseMessage? last = null;

        for (int i = 0; i < 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/Login")
            {
                // ajuste os campos se o seu LoginUserDTO for diferente
                Content = JsonContent.Create(new { Email = "nobody@kairos.test", Password = "Wrong#123" })
            };
            request.Headers.Add("X-Inbox-Id", Guid.NewGuid().ToString());

            last?.Dispose();
            last = await client.SendAsync(request);
        }

        last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        last.Headers.RetryAfter.Should().NotBeNull();

        string body = await last.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":false");
    }
}