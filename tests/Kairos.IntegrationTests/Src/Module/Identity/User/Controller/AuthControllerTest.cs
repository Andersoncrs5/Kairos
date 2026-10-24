using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Res;
using Kairos.Api.Modules.Identity.Auth.Models;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.User.Controller;

[Collection("Integration Tests")]
public class AuthControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;
    public AuthControllerTest(KairosApiFactory factory, ITestOutputHelper output) 
        : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateNewUser_Success()
    {
        long num = Random.Shared.NextInt64(1, 10000000000000);
        string inboxId = Guid.NewGuid().ToString("N");

        var dto = new CreateUserDTO
        {
            Email = $"pochita{num}@gmail.com",
            Password = "test6rA553e463$#%$%",
            UserName = $"pochita{num}",
            FullName = "pochita the chainsaw demon"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/Register")
        {
            Content = JsonContent.Create(dto)
        };
        request.Headers.Add("X-Inbox-Id", inboxId);

        using var response = await Client.SendAsync(request);

        _output.WriteLine("======================================================================");
        _output.WriteLine(response.Content.ReadAsStringAsync().Result);
        _output.WriteLine("======================================================================");

        
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await response.Content.ReadFromJsonAsync<ResponseHttp<Tokens>>();

        http.Should().NotBeNull();
        http!.Data.Should().NotBeNull();
        http.Data!.Token.Should().NotBeNullOrEmpty();
        http.Data.RefreshToken.Should().NotBeNullOrEmpty();
        http.Success.Should().BeTrue();

        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.TraceId.Should().Be(inboxId);
    }

}