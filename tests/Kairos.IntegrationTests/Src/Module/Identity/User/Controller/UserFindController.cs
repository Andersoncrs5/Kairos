using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.User.Controller;

[Collection("Integration Tests")]
public class UserFindController : BaseIntegrationTest
{
    private const string FindByIdUrl = "/api/v1/users";

    private readonly ITestOutputHelper _output;

    public UserFindController(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    private async Task<HttpResponseMessage> SendFindByIdAsync(
        string id,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FindByIdUrl}/{Uri.EscapeDataString(id)}");

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add("X-Inbox-Id", inboxId);
        }

        return await Client.SendAsync(request);
    }

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsOk_WhenUserExists()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            tokens.User.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert - resposta HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<UserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Message.Should().Be("User retrieved successfully");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        // Assert - usuário retornado
        http.Data!.Id.Should().Be(tokens.User.Id);
        http.Data.Email.Should().Be(tokens.User.Email);
        http.Data.UserName.Should().Be(tokens.User.UserName);

        // Assert - inbox processada
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Regras de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();
        string nonexistentId = Guid.NewGuid().ToString("N");

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<UserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("User not found.");
    }

    // ------------------------------------------------------------------
    // Autenticação
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            tokens.User.Id,
            accessToken: null,
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenAccessTokenIsInvalid()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            tokens.User.Id,
            accessToken: "not-a-valid-jwt",
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();

        // Act
        using var response = await SendFindByIdAsync(
            tokens.User.Id,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FindById_ReturnsConflict_WhenInboxIsReused()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act - primeira requisição
        using (var firstResponse = await SendFindByIdAsync(
            tokens.User.Id,
            tokens.Token,
            inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Act - segunda requisição usando a mesma inbox
        using var secondResponse = await SendFindByIdAsync(
            tokens.User.Id,
            tokens.Token,
            inbox.Id);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}