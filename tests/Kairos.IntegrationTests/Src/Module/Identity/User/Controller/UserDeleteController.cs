using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.User.Controller;

[Collection("Integration Tests")]
public class UserDeleteController : BaseIntegrationTest
{
    private const string DeleteUrl = "/api/v1/users";

    private readonly ITestOutputHelper _output;

    public UserDeleteController(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    private async Task<HttpResponseMessage> SendDeleteAsync(
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            DeleteUrl);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

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
    public async Task Delete_ReturnsOk_AndDeletesAuthenticatedUser()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert - resposta HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("User deleted successfully");
        http.TraceId.Should().Be(inbox.Id);

        // Assert - usuário removido do banco
        var deletedUser = await GetUserByEmailAsync(tokens.User.Email);

        deletedUser.Should().BeNull();

        // Assert - inbox processada
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task Delete_DeletesAuthenticatedUser_AndPreservesOtherUsers()
    {
        // Arrange
        Tokens userToDelete = await CreateUserHttp();
        Tokens otherUser = await CreateUserHttp();

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            userToDelete.Token,
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var deletedUser =
            await GetUserByEmailAsync(userToDelete.User.Email);

        var preservedUser =
            await GetUserByEmailAsync(otherUser.User.Email);

        deletedUser.Should().BeNull();
        preservedUser.Should().NotBeNull();
        preservedUser!.Email.Should().Be(otherUser.User.Email);
    }

    // ------------------------------------------------------------------
    // Regras de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenAuthenticatedUserNoLongerExists()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();

        await DeleteUserAsync(tokens.User.Email);

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
    }

    // ------------------------------------------------------------------
    // Autenticação
    // ------------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            accessToken: null,
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // O endpoint não deve excluir o usuário sem autenticação.
        var user = await GetUserByEmailAsync(tokens.User.Email);

        user.Should().NotBeNull();

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenAccessTokenIsInvalid()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            accessToken: "not-a-valid-jwt",
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = await GetUserByEmailAsync(tokens.User.Email);

        user.Should().NotBeNull();

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();

        // Act
        using var response = await SendDeleteAsync(
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var user = await GetUserByEmailAsync(tokens.User.Email);

        user.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReused()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act - primeira requisição
        using (var firstResponse = await SendDeleteAsync(
            tokens.Token,
            inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Act - tenta reutilizar a mesma inbox
        using var secondResponse = await SendDeleteAsync(
            tokens.Token,
            inbox.Id);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}