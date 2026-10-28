using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Auth.Controller.Auth;

[Collection("Integration Tests")]
public sealed class AuthUserExistsByUsernameTest : BaseIntegrationTest
{
    private const string ExistsByUserNameUrl = "/api/v1/auth/Exists/username";

    private readonly ITestOutputHelper _output;

    public AuthUserExistsByUsernameTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    private async Task<HttpResponseMessage> SendExistsByUserNameAsync(
        string userName,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ExistsByUserNameUrl}/{Uri.EscapeDataString(userName)}");

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
    public async Task ExistsByUserName_ReturnsCreatedTrue_WhenUserNameExists()
    {
        // Arrange
        var (registered, _) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        // Act: endpoint público, sem token de acesso.
        using var response = await SendExistsByUserNameAsync(
            registered.UserName,
            inbox.Id);

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

        // Assert: inbox processada
        var processedInbox = await GetInboxAsync(inbox.Id);

        processedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        processedInbox.ProcessedAt.Should().NotBeNull();
        processedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task ExistsByUserName_ReturnsCreatedFalse_WhenUserNameDoesNotExist()
    {
        // Arrange
        string userName = $"missing-{Guid.NewGuid():N}";
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsByUserNameAsync(
            userName,
            inbox.Id);

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

        var processedInbox = await GetInboxAsync(inbox.Id);

        processedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        processedInbox.ProcessedAt.Should().NotBeNull();
        processedInbox.LockedBy.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Normalização
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExistsByUserName_ReturnsTrue_WhenUserNameHasDifferentCasing()
    {
        // Arrange
        var (registered, _) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsByUserNameAsync(
            registered.UserName.ToUpperInvariant(),
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("User exists");

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExistsByUserName_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var (registered, _) = await RegisterUserAsync();

        // Act
        using var response = await SendExistsByUserNameAsync(
            registered.UserName,
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
    public async Task ExistsByUserName_ReturnsConflict_WhenInboxIsReused()
    {
        // Arrange
        var (registered, _) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        // Act: primeira requisição
        using (var firstResponse = await SendExistsByUserNameAsync(
                   registered.UserName,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(
                HttpStatusCode.Created);
        }

        // Act: segunda requisição com a mesma inbox
        using var secondResponse = await SendExistsByUserNameAsync(
            registered.UserName,
            inbox.Id);

        // Assert
        secondResponse.StatusCode.Should().Be(
            HttpStatusCode.Conflict);
    }
}