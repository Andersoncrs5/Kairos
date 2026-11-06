using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Equipment.Controller;

[Collection("Integration Tests")]
public sealed class EquipmentCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/equipments";

    private readonly ITestOutputHelper _output;

    public EquipmentCreateControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsCreated_AndPersistsEquipmentAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewEquipmentDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.Code.Should().Be(dto.Code.Trim());
        http.Data.Name.Should().Be(dto.Name.Trim());
        http.Data.IsActive.Should().BeTrue();

        // Database persistence
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == dto.Code.Trim());

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(http.Data.Id);
        persisted.Code.Should().Be(dto.Code.Trim());
        persisted.Name.Should().Be(dto.Name.Trim());
        persisted.IsActive.Should().BeTrue();
        persisted.Version.Should().Be(1);
        persisted.CreatedAt.Should().NotBe(default);

        await AssertEquipmentInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // DTO validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCodeIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewEquipmentDto(code: "");

        // Act & Assert
        await AssertInvalidDtoAsync(dto, tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewEquipmentDto(name: "");

        // Act & Assert
        await AssertInvalidDtoAsync(dto, tokens.Token);
    }

    // ---------------------------------------------------------------
    // Business rules
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var existingEquipment = await CreateEquipmentHttp(tokens);

        var dto = NewEquipmentDto(
            code: existingEquipment.Code);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            $"Equipment with the code '{dto.Code.Trim()}' already exists.");

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var count = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .CountAsync(item => item.Code == dto.Code.Trim());

        count.Should().Be(1);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenNameAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var existingEquipment = await CreateEquipmentHttp(tokens);

        var dto = NewEquipmentDto(
            name: existingEquipment.Name);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            $"Equipment with the name '{dto.Name.Trim()}' already exists.");

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var count = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .CountAsync(item => item.Name == dto.Name.Trim());

        count.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Inbox and authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewEquipmentDto();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(InboxHeader);

        await AssertNoEquipmentExistsAsync(dto.Code);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var dto = NewEquipmentDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertNoEquipmentExistsAsync(dto.Code);
        await AssertInboxNotCreatedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string inboxId = Guid.NewGuid().ToString();

        var firstDto = NewEquipmentDto();
        var secondDto = NewEquipmentDto();

        // First request
        using (var firstResponse = await SendCreateAsync(
            firstDto,
            inboxId,
            tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Second request reusing the same Inbox ID
        using var response = await SendCreateAsync(
            secondDto,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var firstEquipment = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == firstDto.Code);

        var secondEquipment = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == secondDto.Code);

        firstEquipment.Should().NotBeNull();
        secondEquipment.Should().BeNull();

        await AssertEquipmentInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        CreateEquipmentDto dto,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            CreateUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private async Task AssertInvalidDtoAsync(
        CreateEquipmentDto dto,
        string accessToken)
    {
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(
            dto,
            inboxId,
            accessToken);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertNoEquipmentExistsAsync(dto.Code);
    }

    private static CreateEquipmentDto NewEquipmentDto(
        string? code = null,
        string? name = null)
    {
        return new CreateEquipmentDto
        {
            Code = code ?? $"E{Guid.NewGuid():N}"[..30],
            Name = name ?? $"Integration Equipment {Guid.NewGuid():N}"
        };
    }

    private async Task AssertNoEquipmentExistsAsync(string code)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .AnyAsync(item => item.Code == code);

        exists.Should().BeFalse();
    }

    private async Task AssertEquipmentInboxProcessedAsync(string inboxId)
    {
        var inbox = await GetInboxByMessageIdAsync(inboxId);

        inbox.Should().NotBeNull();
        inbox.MessageId.Should().Be(inboxId);
        inbox.Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Response model used by integration tests
    // ---------------------------------------------------------------
}
