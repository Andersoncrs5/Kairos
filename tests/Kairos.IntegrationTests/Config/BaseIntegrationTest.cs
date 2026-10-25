using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Identity.Auth.Models;
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

namespace Kairos.IntegrationTests.Config;

public abstract class BaseIntegrationTest
{
    protected HttpClient Client { get; }
    protected KairosApiFactory Factory { get; }
    protected const string ValidPassword = "Test123!Abcd";
    
    protected BaseIntegrationTest(KairosApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }
    
    

    protected async Task<Tokens> CreateUserHttp() 
    {
        using var scope = CreateScope();
        var inbox = await CreateInboxAsync(scope.ServiceProvider);

        long num = Random.Shared.NextInt64(1, 10000000000000);

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
    
}