using System.Net;
using FluentAssertions;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Auth.Controller.Auth;

[Collection("Integration Tests")]
public sealed class AuthUserExistsByEmail : BaseIntegrationTest
{
    private const string ExistsByEmailUrl = "/api/v1/auth/Exists/email";

    private readonly ITestOutputHelper _output;

    public AuthUserExistsByEmail(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    private async Task<HttpResponseMessage> SendExistsByEmailAsync(
        string email,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ExistsByEmailUrl}/{Uri.EscapeDataString(email)}");

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExistsByEmail_ReturnsCreatedTrue_WhenEmailExists()
    {
        // Arrange
        var (registered, _) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        // Act: endpoint público, sem token de acesso.
        using var response = await SendExistsByEmailAsync(
            registered.Email,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert: resposta HTTP
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("User exists");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task ExistsByEmail_ReturnsCreatedFalse_WhenEmailDoesNotExist()
    {
        // Arrange
        string email = $"missing-{Guid.NewGuid():N}@example.com";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByEmailAsync(email, inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.Message.Should().Be("User does not exist");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Normalização
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExistsByEmail_ReturnsTrue_WhenEmailHasDifferentCasing()
    {
        // Arrange
        var (registered, _) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByEmailAsync(
            registered.Email.ToUpperInvariant(),
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("User exists");

        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExistsByEmail_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var (registered, _) = await RegisterUserAsync();

        // Act
        using var response = await SendExistsByEmailAsync(
            registered.Email,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(
            error => error.Contains(
                InboxHeader,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExistsByEmail_ReturnsConflict_WhenInboxIsReused()
    {
        // Arrange
        var (registered, _) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        // Act: primeira requisição
        using (var firstResponse = await SendExistsByEmailAsync(
                   registered.Email,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Act: segunda requisição com o mesmo Inbox
        using var secondResponse = await SendExistsByEmailAsync(
            registered.Email,
            inboxId);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
    }
}