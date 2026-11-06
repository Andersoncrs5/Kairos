using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Equipment.Controller;

[Collection("Integration Tests")]
public sealed class EquipmentDeleteControllerTest : BaseIntegrationTest
{
    private const string DeleteUrl = "/api/v1/equipments";

    private readonly ITestOutputHelper _output;

    public EquipmentDeleteControllerTest(KairosApiFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsOk_AndDeletesEquipmentAndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            equipment.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeTrue();

        await AssertEquipmentDoesNotExistAsync(equipment.Id);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();
        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            nonexistentId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------
    // Inbox validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        // Act
        using var response = await SendDeleteAsync(
            equipment.Id,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("X-Inbox-Id");

        await AssertEquipmentExistsAsync(equipment.Id);
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            equipment.Id,
            inboxId,
            accessToken: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertEquipmentExistsAsync(equipment.Id);
    }

    private async Task<HttpResponseMessage> SendDeleteAsync(
        string equipmentId,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{DeleteUrl}/{Uri.EscapeDataString(equipmentId)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (inboxId is not null)
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }

    private async Task AssertEquipmentExistsAsync(string id)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == id);

        exists.Should().BeTrue();
    }

    private async Task AssertEquipmentDoesNotExistAsync(string id)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == id);

        exists.Should().BeFalse();
    }
}