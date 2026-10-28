using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Response;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.PermissionUser.Controller;

[Collection("Integration Tests")]
public class PermissionUserCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/permission-users";

    private readonly ITestOutputHelper _output;

    public PermissionUserCreateControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task Create_ReturnsCreated_AndPersistsPermissionUserAndInbox()
    {
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var permission = await CreatePermissionAsync(scope.ServiceProvider);
        var inbox = await NewInboxAsync();

        var dto = new CreatePermissionUserDTO
        {
            UserId = user.Id,
            PermissionId = permission.Id
        };

        using var response = await SendAsync(
            CreateUrl,
            body: dto,
            accessToken: tokens.Token,
            inboxId: inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.UserId.Should().Be(user.Id);
        http.Data.PermissionId.Should().Be(permission.Id);
        http.Data.IsActive.Should().BeTrue();

        // Verifica a persistência da associação.
        using var verificationScope = CreateScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<PermissionUserModel>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == http.Data.Id);

        persisted.UserId.Should().Be(user.Id);
        persisted.PermissionId.Should().Be(permission.Id);
        persisted.IsActive.Should().BeTrue();

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsForbidden_WhenUserLacksPermission()
    {
        var (_, tokens) = await RegisterUserAsync();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var permission = await CreatePermissionAsync(scope.ServiceProvider);
        var inbox = await NewInboxAsync();

        var dto = new CreatePermissionUserDTO
        {
            UserId = user.Id,
            PermissionId = permission.Id
        };

        using var response = await SendAsync(
            CreateUrl,
            body: dto,
            accessToken: tokens.Token,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var permission = await CreatePermissionAsync(scope.ServiceProvider);

        var dto = new CreatePermissionUserDTO
        {
            UserId = user.Id,
            PermissionId = permission.Id
        };

        using var response = await SendAsync(
            CreateUrl,
            body: dto,
            accessToken: tokens.Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(
            error => error.Contains(InboxHeader));
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var permission = await CreatePermissionAsync(scope.ServiceProvider);

        var dto = new CreatePermissionUserDTO
        {
            UserId = user.Id,
            PermissionId = permission.Id
        };

        using var response = await SendAsync(
            CreateUrl,
            body: dto,
            accessToken: tokens.Token,
            inboxId: Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().NotBeNullOrEmpty();

        using var verificationScope = CreateScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<PermissionUserModel>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == user.Id &&
                x.PermissionId == permission.Id);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenPermissionIdIsNotUuid()
    {
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var inbox = await NewInboxAsync();

        var dto = new CreatePermissionUserDTO
        {
            UserId = user.Id,
            PermissionId = "invalid-uuid"
        };

        using var response = await SendAsync(
            CreateUrl,
            body: dto,
            accessToken: tokens.Token,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_ReturnsNotFound_WhenPermissionDoesNotExist()
    {
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var inbox = await NewInboxAsync();
        var missingPermissionId = Guid.NewGuid().ToString();

        var dto = new CreatePermissionUserDTO
        {
            UserId = user.Id,
            PermissionId = missingPermissionId
        };

        using var response = await SendAsync(
            CreateUrl,
            body: dto,
            accessToken: tokens.Token,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        using var verificationScope = CreateScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<PermissionUserModel>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == user.Id &&
                x.PermissionId == missingPermissionId);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenPermissionIsInactive()
    {
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);

        var permission = await CreatePermissionAsync(
            scope.ServiceProvider,
            p => p.IsActive = false);

        var inbox = await NewInboxAsync();

        var dto = new CreatePermissionUserDTO
        {
            UserId = user.Id,
            PermissionId = permission.Id
        };

        using var response = await SendAsync(
            CreateUrl,
            body: dto,
            accessToken: tokens.Token,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            error => error.Contains(
                "Inactive permissions cannot be assigned to users."));
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenPermissionIsAlreadyAssigned()
    {
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var permission = await CreatePermissionAsync(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingAssociation = new PermissionUserModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            PermissionId = permission.Id,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await db.Set<PermissionUserModel>()
            .AddAsync(existingAssociation);

        await db.SaveChangesAsync();

        var inbox = await NewInboxAsync();

        var dto = new CreatePermissionUserDTO
        {
            UserId = user.Id,
            PermissionId = permission.Id
        };

        using var response = await SendAsync(
            CreateUrl,
            body: dto,
            accessToken: tokens.Token,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            error => error.Contains(
                "This permission is already assigned to the user."));

        var associations = await db.Set<PermissionUserModel>()
            .AsNoTracking()
            .CountAsync(x =>
                x.UserId == user.Id &&
                x.PermissionId == permission.Id);

        associations.Should().Be(1);
    }
}
