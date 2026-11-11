
using FluentAssertions;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Room.RoomAttachment.Repository;

[Collection("Integration Tests")]
public sealed class RoomAttachmentRepositoryTest : BaseIntegrationTest
{
    public RoomAttachmentRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    // ---------------------------------------------------------------
    // GetByStorageKeyAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByStorageKeyAsync_ReturnsAttachment_WhenStorageKeyExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.GetByStorageKeyAsync(
            attachment.StorageKey);

        result.Should().NotBeNull();
        result!.Id.Should().Be(attachment.Id);
        result.StorageKey.Should().Be(attachment.StorageKey);
        result.RoomId.Should().Be(room.Id);
        result.OriginalFileName.Should().Be(attachment.OriginalFileName);
    }

    [Fact]
    public async Task GetByStorageKeyAsync_ReturnsNull_WhenStorageKeyDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.GetByStorageKeyAsync(
            $"rooms/{Guid.NewGuid():N}/missing.jpg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByStorageKeyAsync_ReturnsDetachedEntity_WhenAttachmentExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.GetByStorageKeyAsync(
            attachment.StorageKey);

        result.Should().NotBeNull();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        context.Entry(result!).State.Should().Be(EntityState.Detached);
    }

    // ---------------------------------------------------------------
    // GetByIdWithDetailsAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsAttachment_WhenIdExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.GetByIdWithDetailsAsync(
            attachment.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(attachment.Id);
        result.RoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsRoom_WhenAttachmentExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.GetByIdWithDetailsAsync(
            attachment.Id);

        result.Should().NotBeNull();
        result!.Room.Should().NotBeNull();
        result.Room!.Id.Should().Be(room.Id);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsCreator_WhenAttachmentHasCreator()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var creator = await CreateUserAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        var result = await repository.GetByIdWithDetailsAsync(
            attachment.Id);

        result.Should().NotBeNull();
        result!.Creator.Should().NotBeNull();
        result.Creator!.Id.Should().Be(creator.Id);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsNull_WhenAttachmentDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.GetByIdWithDetailsAsync(
            Guid.NewGuid().ToString());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsDetachedEntity_WhenAttachmentExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var creator = await CreateUserAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        var result = await repository.GetByIdWithDetailsAsync(
            attachment.Id);

        result.Should().NotBeNull();
        result!.Room.Should().NotBeNull();
        result.Creator.Should().NotBeNull();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        context.Entry(result).State.Should().Be(EntityState.Detached);
    }

    // ---------------------------------------------------------------
    // Find - Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var act = async () => await repository.Find(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ---------------------------------------------------------------
    // Find - Identifiers
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByRoomId_WhenRoomIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var expectedRoom = await CreateRoomAsync(
            scope.ServiceProvider);

        var otherRoom = await CreateRoomAsync(
            scope.ServiceProvider);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            expectedRoom);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            otherRoom);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = expectedRoom.Id
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCreatedBy_WhenCreatorIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var creator = await CreateUserAsync(scope.ServiceProvider);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                CreatedBy = creator.Id
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_ReturnsEmptyPage_WhenRoomDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = Guid.NewGuid().ToString()
            });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // Find - Creator
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByCreatorUserName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var creator = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.UserName = $"Creator-{Guid.NewGuid():N}");

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                CreatorUserName = creator.UserName!.ToLowerInvariant(),
                IgnoreCaseCreatorUserName = true
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCreatorUserName_WhenIgnoreCaseIsDisabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var creator = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.UserName = $"Creator-{Guid.NewGuid():N}");

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                CreatorUserName = creator.UserName,
                IgnoreCaseCreatorUserName = false
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCreatorEmail_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var creator = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.Email = $"{Guid.NewGuid():N}@example.com");

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                CreatorEmail = creator.Email!.ToUpperInvariant(),
                IgnoreCaseCreatorEmail = true
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCreatorFullName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var fullName = $"Integration Creator {Guid.NewGuid():N}";

        var creator = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.FullName = fullName);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                CreatorFullName = fullName.ToLowerInvariant(),
                IgnoreCaseCreatorFullName = true
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCreatorPhoneNumber_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var phoneNumber = $"+1555{Random.Shared.Next(1000000, 9999999)}";

        var creator = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.PhoneNumber = phoneNumber);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                CreatorPhoneNumber = phoneNumber,
                IgnoreCaseCreatorPhoneNumber = true
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - File metadata
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByOriginalFileName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var fileName = $"Room-Photo-{Guid.NewGuid():N}.jpg";

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.OriginalFileName = fileName);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                OriginalFileName = fileName.ToLowerInvariant(),
                OriginalFileNameIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByOriginalFileName_WhenIgnoreCaseIsDisabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var fileName = $"Room-Photo-{Guid.NewGuid():N}.jpg";

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.OriginalFileName = fileName);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                OriginalFileName = fileName,
                OriginalFileNameIgnoreCase = false
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByContentType_WhenContentTypeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.ContentType = "image/jpeg");

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.ContentType = "application/pdf");

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                ContentType = "image/jpeg"
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Attachment type
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByType_WhenTypeIsPhoto()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model =>
                model.Type = RoomAttachmentTypeEnum.Photo);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model =>
                model.Type = RoomAttachmentTypeEnum.FloorPlan);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                Type = RoomAttachmentTypeEnum.Photo
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.Data.Should().OnlyContain(
            attachment => attachment.Type == RoomAttachmentTypeEnum.Photo);
    }

    [Fact]
    public async Task Find_FiltersByType_WhenTypeIsFloorPlan()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model =>
            {
                model.Type = RoomAttachmentTypeEnum.FloorPlan;
                model.IsCover = false;
            });

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model =>
                model.Type = RoomAttachmentTypeEnum.Photo);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                Type = RoomAttachmentTypeEnum.FloorPlan
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.Data.Should().OnlyContain(
            attachment => attachment.Type == RoomAttachmentTypeEnum.FloorPlan);
    }

    // ---------------------------------------------------------------
    // Find - Alt text
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByAltText_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var altText = $"Conference room entrance {Guid.NewGuid():N}";

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.AltText = altText);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                AltText = altText.ToLowerInvariant(),
                AltTextIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByAltText_WhenIgnoreCaseIsDisabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var altText = $"Conference room entrance {Guid.NewGuid():N}";

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.AltText = altText);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                AltText = altText,
                AltTextIgnoreCase = false
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);
    }

    // ---------------------------------------------------------------
    // Find - Cover
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByIsCover_WhenIsCoverIsTrue()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var cover = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model =>
            {
                model.Type = RoomAttachmentTypeEnum.Photo;
                model.IsCover = true;
            });

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.IsCover = false);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                IsCover = true
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == cover.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByIsCover_WhenIsCoverIsFalse()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.IsCover = true);

        var nonCover = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.IsCover = false);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                IsCover = false
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == nonCover.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Sort order
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByMinSortOrder_WhenMinSortOrderIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 1);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 5);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                MinSortOrder = 5
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByMaxSortOrder_WhenMaxSortOrderIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 2);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 8);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                MaxSortOrder = 2
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersBySortOrderRange_WhenMinAndMaxAreProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 1);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 5);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 10);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                MinSortOrder = 3,
                MaxSortOrder = 7
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Creation date
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByCreatedAtRange_WhenDateRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-5);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: targetDate);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: targetDate.AddDays(-5));

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: targetDate.AddDays(5));

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                CreatedAtFrom = targetDate.AddMinutes(-1),
                CreatedAtTo = targetDate.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - File size
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByMinSizeBytes_WhenMinimumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SizeBytes = 1024);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SizeBytes = 8192);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                MinSizeBytes = 8192
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByMaxSizeBytes_WhenMaximumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SizeBytes = 1024);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SizeBytes = 8192);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                MaxSizeBytes = 1024
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersBySizeRange_WhenMinimumAndMaximumAreProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SizeBytes = 512);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SizeBytes = 4096);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SizeBytes = 16384);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                MinSizeBytes = 2048,
                MaxSizeBytes = 8192
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - BaseFilter
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersById_WhenIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                Id = expectedAttachment.Id
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByVersionRange_WhenVersionRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            version: 2);

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            version: 5);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            version: 10);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                VersionMin = 4,
                VersionMax = 6
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByBaseCreatedAtRange_WhenRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-10);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: targetDate.AddDays(-5));

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: targetDate);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: targetDate.AddDays(5));

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                CreatedAtMin = targetDate.AddMinutes(-1),
                CreatedAtMax = targetDate.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByUpdatedAtRange_WhenRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-10);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            updatedAt: targetDate.AddDays(-5));

        var expectedAttachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            updatedAt: targetDate);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            updatedAt: targetDate.AddDays(5));

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                UpdatedAtMin = targetDate.AddMinutes(-1),
                UpdatedAtMax = targetDate.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == expectedAttachment.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Relationships
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_IncludesRoom_WhenAttachmentExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id
            });

        result.Data.Should().ContainSingle();

        var returnedAttachment = result.Data.Single();

        returnedAttachment.Id.Should().Be(attachment.Id);
        returnedAttachment.Room.Should().NotBeNull();
        returnedAttachment.Room!.Id.Should().Be(room.Id);
    }

    [Fact]
    public async Task Find_IncludesCreator_WhenAttachmentHasCreator()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var creator = await CreateUserAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdBy: creator.Id);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id
            });

        result.Data.Should().ContainSingle();

        var returnedAttachment = result.Data.Single();

        returnedAttachment.Id.Should().Be(attachment.Id);
        returnedAttachment.Creator.Should().NotBeNull();
        returnedAttachment.Creator!.Id.Should().Be(creator.Id);
    }

    // ---------------------------------------------------------------
    // Find - Pagination
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsPaginatedResults_WhenPageSizeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var first = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 1);

        var second = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 2);

        var third = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 3);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                Page = 1,
                PageSize = 2,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(2);

        result.Data.Select(attachment => attachment.Id)
            .Should()
            .ContainInOrder(first.Id, second.Id);

        result.Data.Should().NotContain(
            attachment => attachment.Id == third.Id);
    }

    [Fact]
    public async Task Find_ReturnsSecondPage_WhenPageNumberIsTwo()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var first = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 1);

        var second = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 2);

        var third = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            configure: model => model.SortOrder = 3);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                Page = 2,
                PageSize = 2,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Should().ContainSingle(
            attachment => attachment.Id == third.Id);

        result.TotalCount.Should().Be(3);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(2);

        result.Data.Should().NotContain(
            attachment => attachment.Id == first.Id);

        result.Data.Should().NotContain(
            attachment => attachment.Id == second.Id);
    }

    [Fact]
    public async Task Find_ReturnsDefaultPagination_WhenPageAndPageSizeAreNotProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);

        result.TotalCount.Should().Be(1);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Find_ClampsPageNumberToOne_WhenPageIsZero()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                Page = 0
            });

        result.PageNumber.Should().Be(1);

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_ClampsPageSizeToOneHundred_WhenPageSizeExceedsMaximum()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                Page = 1,
                PageSize = 1000
            });

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Find_ReturnsEmptyPage_WhenRequestedPageDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                Page = 10,
                PageSize = 20
            });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
        result.PageNumber.Should().Be(10);
    }

    // ---------------------------------------------------------------
    // Find - Deterministic ordering
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_OrdersBySortOrderAscending_WhenColumnOrderIsAsc()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var baseDate = DateTimeOffset.UtcNow.AddDays(-10);

        var first = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: baseDate,
            configure: model => model.SortOrder = 1);

        var second = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: baseDate.AddDays(1),
            configure: model => model.SortOrder = 1);

        var third = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: baseDate.AddDays(2),
            configure: model => model.SortOrder = 2);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Should().HaveCount(3);

        result.Data.Select(attachment => attachment.Id)
            .Should()
            .ContainInOrder(first.Id, second.Id, third.Id);
    }

    [Fact]
    public async Task Find_OrdersByCreatedAtDescendingWithinSortOrder_WhenColumnOrderIsDesc()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var baseDate = DateTimeOffset.UtcNow.AddDays(-10);

        var oldest = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: baseDate,
            configure: model => model.SortOrder = 1);

        var newest = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: baseDate.AddDays(2),
            configure: model => model.SortOrder = 1);

        var laterSortOrder = await CreateAttachmentAsync(
            scope.ServiceProvider,
            room,
            createdAt: baseDate.AddDays(5),
            configure: model => model.SortOrder = 2);

        var result = await repository.Find(
            new FilterRoomAttachmentDto
            {
                RoomId = room.Id,
                ColumnOrder = ColumnOrderEnum.DESC
            });

        result.Data.Should().HaveCount(3);

        // SortOrder remains ascending.
        // CreatedAt is descending inside the same SortOrder.
        result.Data.Select(attachment => attachment.Id)
            .Should()
            .ContainInOrder(
                newest.Id,
                oldest.Id,
                laterSortOrder.Id);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static IRoomAttachmentRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IRoomAttachmentRepository>();
    }

    private static async Task<RoomAttachmentModel> CreateAttachmentAsync(
        IServiceProvider services,
        RoomModel room,
        string? createdBy = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        long? version = null,
        Action<RoomAttachmentModel>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(room);

        var uniqueId = Guid.NewGuid().ToString("N");

        var attachment = new RoomAttachmentModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = room.Id,

            Type = RoomAttachmentTypeEnum.Photo,

            StorageKey = $"rooms/{room.Id}/{uniqueId}/attachment.jpg",
            OriginalFileName = $"attachment-{uniqueId}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            Sha256 = new string('A', 64),

            AltText = $"Integration test attachment {uniqueId}",
            SortOrder = 0,
            IsCover = false,

            CreatedBy = createdBy,

            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = updatedAt,
            Version = version ?? 1
        };

        configure?.Invoke(attachment);

        var context = services.GetRequiredService<AppDbContext>();

        await context.Set<RoomAttachmentModel>()
            .AddAsync(attachment);

        await context.SaveChangesAsync();

        // SaveChanges may update audit fields and Version.
        // Apply explicit values afterward to keep filtering tests deterministic.
        await SetBaseValuesAsync(
            context,
            attachment.Id,
            createdAt,
            updatedAt,
            version);

        return attachment;
    }

    private static async Task SetBaseValuesAsync(
        AppDbContext context,
        string attachmentId,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        long? version = null)
    {
        var query = context.Set<RoomAttachmentModel>()
            .Where(attachment => attachment.Id == attachmentId);

        if (createdAt.HasValue)
        {
            DateTimeOffset value = createdAt.Value;

            await query.ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    attachment => attachment.CreatedAt,
                    value));
        }

        if (updatedAt.HasValue)
        {
            DateTimeOffset value = updatedAt.Value;

            await query.ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    attachment => attachment.UpdatedAt,
                    value));
        }

        if (version.HasValue)
        {
            long value = version.Value;

            await query.ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    attachment => attachment.Version,
                    value));
        }
    }
}
