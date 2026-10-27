using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kairos.IntegrationTests.Config;

public abstract class BaseIntegrationTest
{
    protected HttpClient Client { get; }
    protected IOptions<UserMasterOptions> UserMasterOptions { get; }
    protected KairosApiFactory Factory { get; }
    protected const string ValidPassword = "Test123!Abcd";
    protected const string TestPassword = "test6rA553e463$#%$%";
    protected const string InboxHeader = "X-Inbox-Id";
    protected const string RegisterUrl = "/api/v1/auth/Register";
    protected const string LoginUrl = "/api/v1/auth/Login";
    protected const string LogoutUrl = "/api/v1/auth/Logout";
    protected const string ChangePasswordUrl = "/api/v1/auth/ChangePassword";
    
    protected BaseIntegrationTest(KairosApiFactory factory)
        : this(
            factory,
            factory.Services
                .GetRequiredService<IOptions<UserMasterOptions>>())
    {
    }

    protected BaseIntegrationTest(
        KairosApiFactory factory,
        IOptions<UserMasterOptions> userMasterOptions)
    {
        Factory = factory;
        Client = factory.CreateClient();
        UserMasterOptions = userMasterOptions;
    }
    
    protected async Task<Tokens> LoginUserMaster()
    {
        using var scope = CreateScope();
        var inbox = await CreateInboxAsync(scope.ServiceProvider);

        var dto = new LoginUserDTO()
        {
            Email = UserMasterOptions.Value.Email,
            Password = UserMasterOptions.Value.Password,
        };
        
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/Login")
        {
            Content = JsonContent.Create(dto)
        };
        request.Headers.Add("X-Inbox-Id", inbox.Id);

        using var response = await Client.SendAsync(request);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await response.Content.ReadFromJsonAsync<ResponseHttp<Tokens>>();

        http.Should().NotBeNull();
        http!.Data.Should().NotBeNull();
        http.Data!.Token.Should().NotBeNullOrEmpty();
        
        http.Data!.User.Email.Should().NotBeNullOrEmpty();
        http.Data!.User.UserName.Should().Be(UserMasterOptions.Value.UserName);
        http.Data!.User.Email.Should().Be(UserMasterOptions.Value.Email);
        
        http.Data.RefreshToken.Should().NotBeNullOrEmpty();
        http.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(i => i.Id == inbox.Id);

        updated.Status.Should().Be(InboxStatusEnum.Processed);
        updated.LockedBy.Should().BeNull();
        updated.ProcessedAt.Should().NotBeNull();

        return http.Data;
    }
    
    protected static Task<ResponseHttp<T>?> ReadAsync<T>(HttpResponseMessage response)
        => response.Content.ReadFromJsonAsync<ResponseHttp<T>>();
    
    protected async Task<InboxModel> GetInboxAsync(string id)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(i => i.Id == id);
    }
    
    protected async Task<UserModel?> GetUserByEmailAsync(string email)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserModel>>();

        return await userManager.FindByEmailAsync(email);
    }

    protected async Task<Tokens> CreateUserHttp() 
    {
        using var scope = CreateScope();
        var inbox = await CreateInboxAsync(scope.ServiceProvider);

        long num = Random.Shared.NextInt64(1, 10000000000000);

        var dto = new CreateUserDTO
        {
            Email = $"pochita{num}@gmail.com",
            Password = TestPassword,
            UserName = $"pochita{num}",
            FullName = "pochita the chainsaw demon"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/Register")
        {
            Content = JsonContent.Create(dto)
        };
        request.Headers.Add("X-Inbox-Id", inbox.Id);

        using var response = await Client.SendAsync(request);
        
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await response.Content.ReadFromJsonAsync<ResponseHttp<Tokens>>();

        http.Should().NotBeNull();
        http!.Data.Should().NotBeNull();
        http.Data!.Token.Should().NotBeNullOrEmpty();
        http.Data.RefreshToken.Should().NotBeNullOrEmpty();
        http.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(i => i.Id == inbox.Id);

        updated.Status.Should().Be(InboxStatusEnum.Processed);
        updated.LockedBy.Should().BeNull();
        updated.ProcessedAt.Should().NotBeNull();

        return http.Data;
    }
    
    protected async Task<InboxModel> NewInboxAsync(Action<InboxModel>? configure = null)
    {
        using var scope = CreateScope();
        return await CreateInboxAsync(scope.ServiceProvider, configure);
    }
    
    
    
    protected static async Task<InboxModel> CreateInboxAsync(
        IServiceProvider services,
        Action<InboxModel>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var inbox = new InboxModel
        {
            Id = Guid.NewGuid().ToString(),
            MessageId = $"message-{Guid.NewGuid():N}",
            MessageType = "auth.register",
            Payload = """{"value":"integration-test"}""",
            Status = InboxStatusEnum.Pending,
            ReceivedAt = now,
            RetryCount = 0,
            CreatedAt = now
        };

        configure?.Invoke(inbox);

        await dbContext.Set<InboxModel>().AddAsync(inbox, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return inbox;
    }

    protected IServiceScope CreateScope()
    {
        return Factory.Services.CreateScope();
    }

    protected static IUserRepository GetUserRepository(IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IUserRepository>();
    }
    
    protected static IPermissionRepository GetPermissionRepository(IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IPermissionRepository>();
    }
    
    protected static IPermissionUserRepository GetPermissionUserRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IPermissionUserRepository>();
    }

    protected async Task<PermissionUserModel> CreatePermissionUserAsync(
        IServiceProvider services,
        bool isActive = true)
    {
        var user = await CreateUserAsync(services);
        var permission = await CreatePermissionAsync(services);

        var dbContext = services.GetRequiredService<AppDbContext>();

        var permissionUser = new PermissionUserModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            PermissionId = permission.Id,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Set<PermissionUserModel>()
            .AddAsync(permissionUser);

        await dbContext.SaveChangesAsync();

        return permissionUser;
    }
    
    protected static async Task<UserModel> CreateUserAsync(
        IServiceProvider services,
        Action<UserModel>? configure = null)
    {
        var userManager = services.GetRequiredService<UserManager<UserModel>>();
        var uniqueId = Guid.NewGuid().ToString("N");

        var user = new UserModel
        {
            UserName = $"user-{uniqueId}",
            Email = $"{uniqueId}@example.com",
            FullName = "Integration Test",
            CreatedAt = DateTimeOffset.UtcNow,
            Permissions = []
        };

        configure?.Invoke(user);

        var result = await userManager.CreateAsync(user, ValidPassword);

        result.Succeeded.Should().BeTrue(
            string.Join("; ", result.Errors.Select(error => error.Description)));

        return user;
    }
    
    protected static async Task<PermissionsModel> CreatePermissionAsync(
        IServiceProvider services,
        Action<PermissionsModel>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N");

        var permission = new PermissionsModel
        {
            Id = Guid.NewGuid().ToString(),
            Code = $"test.permission.{uniqueId}",
            Name = $"Integration Permission {uniqueId}",
            Description = "Permission created by integration test.",
            IsActive = true,
            IsSystem = false,
            Module = PermissionModuleEnum.Identity,
            CreatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(permission);

        await dbContext.Permissions.AddAsync(
            permission,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return permission;
    }
    
    protected static async Task<CategoryModel> CreateCategoryAsync(
        IServiceProvider services,
        Action<CategoryModel>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N");

        var category = new CategoryModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Integration Category {uniqueId}",
            Description = "Category created by integration test.",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(category);

        await dbContext.Categories.AddAsync(category, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return category;
    }
    
    protected static async Task<RoomTypeModel> CreateRoomTypeAsync(
        IServiceProvider services,
        Action<RoomTypeModel>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N");

        var roomType = new RoomTypeModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Integration Room Type {uniqueId}",
            Description = "Room type created by integration test.",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(roomType);

        await dbContext.Set<RoomTypeModel>()
            .AddAsync(roomType, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return roomType;
    }
    
    protected static async Task<RoomModel> CreateRoomAsync(
        IServiceProvider services,
        Action<RoomModel>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var roomType = new RoomTypeModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Integration Room Type {uniqueId}",
            Description = "Room type created by integration test.",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var room = new RoomModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomTypeId = roomType.Id,
            Name = $"Integration Room {uniqueId}",
            Location = "Integration Test Location",
            Capacity = 10,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        configure?.Invoke(room);

        await dbContext.Set<RoomTypeModel>()
            .AddAsync(roomType, cancellationToken);

        await dbContext.Set<RoomModel>()
            .AddAsync(room, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return room;
    }
    
    protected static async Task<RoomBlockoutModel> CreateRoomBlockoutAsync(
        IServiceProvider services,
        string? roomId = null,
        string? createdBy = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        Action<RoomBlockoutModel>? configure = null,
        CancellationToken cancellationToken = default)
    {
        roomId ??= (await CreateRoomAsync(services)).Id;
        createdBy ??= (await CreateUserAsync(services)).Id;

        var start = startsAt ?? DateTimeOffset.UtcNow.AddDays(3);

        var blockout = new RoomBlockoutModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            Reason = $"Integration blockout {Guid.NewGuid():N}",
            StartsAt = start,
            EndsAt = endsAt ?? start.AddHours(1),
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(blockout);

        var dbContext = services.GetRequiredService<AppDbContext>();

        await dbContext.Set<RoomBlockoutModel>()
            .AddAsync(blockout, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return blockout;
    }
    
    protected static string TamperSignature(string jwt)
    {
        var parts = jwt.Split('.');
        var signature = parts[2];

        parts[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];

        return string.Join('.', parts);
    }
    
    // ------------------------------------------------------------------
    // HTTP genérico
    // ------------------------------------------------------------------

    protected async Task<HttpResponseMessage> SendAsync(
        string url,
        object? body = null,
        string? accessToken = null,
        string? inboxId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);

        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType());

        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (inboxId is not null)
            request.Headers.Add(InboxHeader, inboxId);

        return await Client.SendAsync(request);
    }

    // ------------------------------------------------------------------
    // Auth: requisições
    // ------------------------------------------------------------------

    protected Task<HttpResponseMessage> SendRegisterAsync(CreateUserDTO dto, string? inboxId)
        => SendAsync(RegisterUrl, dto, inboxId: inboxId);

    protected Task<HttpResponseMessage> SendLoginAsync(object body, string? inboxId)
        => SendAsync(LoginUrl, body, inboxId: inboxId);

    protected Task<HttpResponseMessage> SendLogoutAsync(string? accessToken, string? inboxId)
        => SendAsync(LogoutUrl, accessToken: accessToken, inboxId: inboxId);

    protected Task<HttpResponseMessage> SendChangePasswordAsync(
        ChangePasswordUserDto dto,
        string? accessToken,
        string? inboxId)
        => SendAsync(ChangePasswordUrl, dto, accessToken, inboxId);

    protected async Task<HttpResponseMessage> LoginWithNewInboxAsync(string email, string password)
    {
        var inbox = await NewInboxAsync();

        return await SendLoginAsync(
            new LoginUserDTO { Email = email, Password = password },
            inbox.Id);
    }

    // ------------------------------------------------------------------
    // Auth: dados de teste
    // ------------------------------------------------------------------

    protected static CreateUserDTO NewUserDto(
        string? userName = null,
        string? email = null,
        string? password = null,
        string? fullName = "Pochita the Chainsaw Demon")
    {
        var id = Guid.NewGuid().ToString("N");

        return new CreateUserDTO
        {
            UserName = userName ?? $"pochita{id}",
            Email = email ?? $"pochita{id}@example.com",
            Password = password ?? ValidPassword,
            FullName = fullName
        };
    }

    /// <summary>Cadastra um usuário novo via HTTP e devolve o DTO usado e os tokens recebidos.</summary>
    protected async Task<(CreateUserDTO Dto, Tokens Tokens)> RegisterUserAsync()
    {
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, (await NewInboxAsync()).Id);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<Tokens>(response);

        return (dto, http!.Data!);
    }

    // ------------------------------------------------------------------
    // Auth: acesso direto ao banco
    // ------------------------------------------------------------------

    protected async Task<bool> UserExistsAsync(string email)
    {
        using var scope = CreateScope();
        return await GetUserRepository(scope).ExistsByEmail(email);
    }

    protected async Task UpdateUserAsync(string email, Action<UserModel> configure)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserModel>>();

        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();

        configure(user!);

        (await userManager.UpdateAsync(user!)).Succeeded.Should().BeTrue();
    }

    protected async Task DeleteUserAsync(string email)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserModel>>();

        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();

        (await userManager.DeleteAsync(user!)).Succeeded.Should().BeTrue();
    }
    
}