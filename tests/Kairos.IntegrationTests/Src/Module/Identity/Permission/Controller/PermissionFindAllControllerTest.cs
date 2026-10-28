using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Permission.Controller;

[Collection("Integration Tests")]
public sealed class PermissionFindAllControllerTest : BaseIntegrationTest
{
    private const string ExistingPermissionCode = "identity.permissions.create";

    private readonly ITestOutputHelper _output;

    public PermissionFindAllControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Listagem
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsOk_WithPaginationMetadata()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        using var response = await SendFindAsync(
            "Page=1&PageSize=10",
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Permissions retrieved successfully");
        http.Data.Should().NotBeNull();

        http.Data!.PageNumber.Should().Be(1);
        http.Data.PageSize.Should().Be(10);
        http.Data.Count.Should().Be(http.Data.Data.Count);
        http.Data.TotalCount.Should().BeGreaterThan(0);
        http.Data.TotalCount.Should().BeGreaterThanOrEqualTo(
            http.Data.Count);

        http.Data.Data.Should().NotBeEmpty();
    }

    // ---------------------------------------------------------------
    // Filtro por código
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByCode_WhenCodeExists()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var query =
            $"Code={Uri.EscapeDataString(ExistingPermissionCode)}" +
            "&Page=1&PageSize=10";

        using var response = await SendFindAsync(
            query,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Data.Should().ContainSingle(permission =>
            permission.Code == ExistingPermissionCode);

        http.Data.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCodeIgnoringCase_WhenEnabled()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var code = ExistingPermissionCode.ToUpperInvariant();

        var query =
            $"Code={Uri.EscapeDataString(code)}" +
            "&CodeIgnoreCase=true&Page=1&PageSize=10";

        using var response = await SendFindAsync(
            query,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Data.Should().Contain(permission =>
            permission.Code == ExistingPermissionCode);
    }

    [Fact]
    public async Task Find_ReturnsEmptyPage_WhenNoPermissionMatches()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var nonexistentCode = $"missing.permission.{Guid.NewGuid():N}";

        var query =
            $"Code={Uri.EscapeDataString(nonexistentCode)}" +
            "&Page=1&PageSize=10";

        using var response = await SendFindAsync(
            query,
            tokens.Token,
            inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Data.Should().BeEmpty();
        http.Data.Count.Should().Be(0);
        http.Data.TotalCount.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // Paginação
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_RespectsPageSize()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        using var response = await SendFindAsync(
            "Page=1&PageSize=2",
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.PageNumber.Should().Be(1);
        http.Data.PageSize.Should().Be(2);
        http.Data.Data.Should().HaveCountLessThanOrEqualTo(2);
        http.Data.Count.Should().Be(http.Data.Data.Count);
    }

    [Fact]
    public async Task Find_ReturnsRequestedPage()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        using var firstResponse = await SendFindAsync(
            "Page=1&PageSize=2",
            tokens.Token,
            inbox.Id);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPage =
            await ReadAsync<PageResponse<PermissionDto>>(firstResponse);

        firstPage.Should().NotBeNull();
        firstPage!.Data.Should().NotBeNull();

        var secondInbox = await NewInboxAsync();

        using var secondResponse = await SendFindAsync(
            "Page=2&PageSize=2",
            tokens.Token,
            secondInbox.Id);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPage =
            await ReadAsync<PageResponse<PermissionDto>>(secondResponse);

        secondPage.Should().NotBeNull();
        secondPage!.Data.Should().NotBeNull();
        secondPage.Data!.PageNumber.Should().Be(2);
        secondPage.Data.PageSize.Should().Be(2);

        // Como há várias permissões seed, esperamos registros
        // distintos entre as duas páginas.
        var firstIds = firstPage.Data!.Data
            .Select(permission => permission.Id)
            .ToHashSet();

        secondPage.Data.Data
            .Select(permission => permission.Id)
            .Should()
            .NotContain(id => firstIds.Contains(id));
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var inbox = await NewInboxAsync();

        using var response = await SendFindAsync(
            "Page=1&PageSize=10",
            accessToken: null,
            inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Find_ReturnsForbidden_WhenUserLacksListPermission()
    {
        var (_, tokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendFindAsync(
            "Page=1&PageSize=10",
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------
    // HTTP helper
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindAsync(
        string query,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/permissions?{query}");

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
}