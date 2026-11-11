using FluentAssertions;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.Reservations.ReservationAttachment.Repository;

[Collection("Integration Tests")]
public sealed class ReservationAttachmentRepositoryTest : BaseIntegrationTest
{
    public ReservationAttachmentRepositoryTest(KairosApiFactory factory)
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
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.GetByStorageKeyAsync(
            attachment.StorageKey);

        result.Should().NotBeNull();
        result!.Id.Should().Be(attachment.Id);
        result.StorageKey.Should().Be(attachment.StorageKey);
        result.ReservationId.Should().Be(reservation.Id);
    }

    [Fact]
    public async Task GetByStorageKeyAsync_ReturnsNull_WhenStorageKeyDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.GetByStorageKeyAsync(
            $"missing/{Guid.NewGuid():N}/attachment.pdf");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByStorageKeyAsync_ReturnsDetachedEntity_WhenAttachmentExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

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
    public async Task GetByIdWithDetailsAsync_ReturnsAttachmentWithReservationAndUploader()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.GetByIdWithDetailsAsync(
            attachment.Id);

        result.Should().NotBeNull();

        result!.Id.Should().Be(attachment.Id);
        result.ReservationId.Should().Be(reservation.Id);

        result.Reservation.Should().NotBeNull();
        result.Reservation!.Id.Should().Be(reservation.Id);

        result.Uploader.Should().NotBeNull();
        result.Uploader!.Id.Should().Be(attachment.UploadedBy);
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
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.GetByIdWithDetailsAsync(
            attachment.Id);

        result.Should().NotBeNull();
        result!.Reservation.Should().NotBeNull();
        result.Uploader.Should().NotBeNull();

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
    public async Task Find_FiltersByReservationId_WhenReservationIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        // Another reservation must not appear in the result.
        var anotherReservation = await CreateReservationAsync(
            scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            anotherReservation);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id
            });

        result.Data.Should().ContainSingle();

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByUploadedBy_WhenUploaderIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var uploader = await CreateUserAsync(scope.ServiceProvider);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            userId: uploader.Id);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            uploadedBy: uploader.Id);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                UploadedBy = uploader.Id
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_ReturnsEmptyPage_WhenReservationIdDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = Guid.NewGuid().ToString()
            });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // Find - Reservation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByReservationTitle_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var title = $"Integration Reservation {Guid.NewGuid():N}";

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            configure: model => model.Title = title);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                ReservationTitle = title.ToLowerInvariant(),
                ReservationTitleIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByReservationTitle_WhenIgnoreCaseIsDisabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var title = $"Integration Reservation {Guid.NewGuid():N}";

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            configure: model => model.Title = title);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                ReservationTitle = title,
                ReservationTitleIgnoreCase = false
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    // ---------------------------------------------------------------
    // Find - Uploader
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByUploaderUserName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var uploader = await CreateUserAsync(
            scope.ServiceProvider,
            model =>
            {
                model.UserName = $"Uploader-{Guid.NewGuid():N}";
            });

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            userId: uploader.Id);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            uploadedBy: uploader.Id);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                UploadedUserName = uploader.UserName!.ToLowerInvariant(),
                IgnoreCaseUploadedUserName = true
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByUploaderEmail_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var uploader = await CreateUserAsync(
            scope.ServiceProvider,
            model =>
            {
                model.Email = $"{Guid.NewGuid():N}@example.com";
            });

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            userId: uploader.Id);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            uploadedBy: uploader.Id);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                UploadedEmail = uploader.Email!.ToUpperInvariant(),
                IgnoreCaseUploadedEmail = true
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByUploaderFullName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var fullName = $"Integration Uploader {Guid.NewGuid():N}";

        var uploader = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.FullName = fullName);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            userId: uploader.Id);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            uploadedBy: uploader.Id);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                UploadedFullName = fullName.ToLowerInvariant(),
                IgnoreCaseUploadedFullName = true
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByUploaderPhoneNumber_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var phoneNumber = $"+1555{Random.Shared.Next(1000000, 9999999)}";

        var uploader = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.PhoneNumber = phoneNumber);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            userId: uploader.Id);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            uploadedBy: uploader.Id);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                UploadedPhoneNumber = phoneNumber,
                IgnoreCaseUploadedPhoneNumber = true
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    // ---------------------------------------------------------------
    // Find - File metadata
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByOriginalFileName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var fileName = $"Annual-Report-{Guid.NewGuid():N}.pdf";

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.OriginalFileName = fileName);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                OriginalFileName = fileName.ToLowerInvariant(),
                OriginalFileNameIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByDisplayName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var displayName = $"Important Document {Guid.NewGuid():N}";

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.DisplayName = displayName);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                DisplayName = displayName.ToLowerInvariant(),
                DisplayNameIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByContentType_WhenContentTypeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.ContentType = "application/pdf");

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                ContentType = "application/pdf"
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_ReturnsEmptyPage_WhenContentTypeDoesNotMatch()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.ContentType = "application/pdf");

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                ContentType = "application/zip"
            });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // Find - Visibility and scan status
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByVisibility_WhenVisibilityIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model =>
                model.Visibility = ReservationAttachmentVisibilityEnum.Participants);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                Visibility = ReservationAttachmentVisibilityEnum.Participants
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByScanStatus_WhenScanStatusIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model =>
                model.ScanStatus = FileScanStatusEnum.NotScanned);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                ScanStatus = FileScanStatusEnum.NotScanned
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    // ---------------------------------------------------------------
    // Find - Creation date and file size
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByCreatedAtRange_WhenDateRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                CreatedAtFrom = attachment.CreatedAt.AddMinutes(-1),
                CreatedAtTo = attachment.CreatedAt.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByMinimumSize_WhenMinSizeBytesIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.SizeBytes = 8192);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                MinSizeBytes = 8192
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    [Fact]
    public async Task Find_FiltersByMaximumSize_WhenMaxSizeBytesIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.SizeBytes = 8192);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                MaxSizeBytes = 8192
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    // ---------------------------------------------------------------
    // Find - Description
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersAttachmentsWithDescription_WhenHasDescriptionIsTrue()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachmentWithDescription = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.Description = "An attachment description.");

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.Description = null);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                HasDescription = true
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachmentWithDescription.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersAttachmentsWithoutDescription_WhenHasDescriptionIsFalse()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.Description = "An attachment description.");

        var attachmentWithoutDescription = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model => model.Description = null);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                HasDescription = false
            });

        result.Data.Should().ContainSingle(
            item => item.Id == attachmentWithoutDescription.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Pagination and ordering
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsPaginatedResults_WhenPageSizeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var first = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var second = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var third = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                Page = 1,
                PageSize = 2,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(2);

        result.Data.Select(item => item.Id)
            .Should()
            .Contain(first.Id);

        result.Data.Select(item => item.Id)
            .Should()
            .Contain(second.Id);

        result.Data.Should().NotContain(
            item => item.Id == third.Id);
    }

    [Fact]
    public async Task Find_ReturnsSecondPage_WhenRequestedPageIsTwo()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachments = new List<ReservationAttachmentModel>();

        for (var index = 0; index < 3; index++)
        {
            attachments.Add(
                await CreateAttachmentAsync(
                    scope.ServiceProvider,
                    reservation,
                    configure: model =>
                    {
                        model.CreatedAt = DateTimeOffset.UtcNow
                            .AddMinutes(index - 3);
                    }));
        }

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                Page = 2,
                PageSize = 2,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Should().ContainSingle();
        result.TotalCount.Should().Be(3);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(2);

        result.Data.Single().Id.Should().Be(attachments[2].Id);
    }

    [Fact]
    public async Task Find_OrdersByCreatedAtDescending_WhenColumnOrderIsDesc()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var oldest = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model =>
                model.CreatedAt = DateTimeOffset.UtcNow.AddDays(-3));

        var newest = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model =>
                model.CreatedAt = DateTimeOffset.UtcNow.AddDays(-1));

        var middle = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation,
            configure: model =>
                model.CreatedAt = DateTimeOffset.UtcNow.AddDays(-2));

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                ColumnOrder = ColumnOrderEnum.DESC
            });

        result.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task Find_UsesDefaultPagination_WhenPageAndPageSizeAreNotProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id
            });

        result.Data.Should().ContainSingle();
        result.TotalCount.Should().Be(1);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Find_ClampsPageSizeToMaximum_WhenPageSizeExceedsLimit()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                Page = 1,
                PageSize = 1000
            });

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Find_UsesFirstPage_WhenPageNumberIsLessThanOne()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);
        var reservation = await CreateReservationAsync(scope.ServiceProvider);

        var attachment = await CreateAttachmentAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttachmentDto
            {
                ReservationId = reservation.Id,
                Page = 0
            });

        result.PageNumber.Should().Be(1);

        result.Data.Should().ContainSingle(
            item => item.Id == attachment.Id);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static IReservationAttachmentRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IReservationAttachmentRepository>();
    }

    private async Task<ReservationModel> CreateReservationAsync(
        IServiceProvider services,
        string? userId = null,
        Action<ReservationModel>? configure = null)
    {
        var user = userId is null
            ? await CreateUserAsync(services)
            : null;

        var room = await CreateRoomAsync(services);
        var category = await CreateCategoryAsync(services);

        var startsAt = DateTimeOffset.UtcNow.AddDays(2);

        var reservation = new ReservationModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = room.Id,
            UserId = userId ?? user!.Id,
            CategoryId = category.Id,
            Title = $"Integration Reservation {Guid.NewGuid():N}",
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(1),
            Status = ReservationStatusEnum.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(reservation);

        var context = services.GetRequiredService<AppDbContext>();

        await context.Set<ReservationModel>()
            .AddAsync(reservation);

        await context.SaveChangesAsync();

        return reservation;
    }

    private static async Task<ReservationAttachmentModel> CreateAttachmentAsync(
        IServiceProvider services,
        ReservationModel reservation,
        string? uploadedBy = null,
        Action<ReservationAttachmentModel>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var uniqueId = Guid.NewGuid().ToString("N");

        var attachment = new ReservationAttachmentModel
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = reservation.Id,

            StorageKey = $"reservations/{uniqueId}/attachment.pdf",
            OriginalFileName = $"attachment-{uniqueId}.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            Sha256 = new string('A', 64),

            DisplayName = $"Integration Attachment {uniqueId}",
            Description = "Integration test attachment",

            Visibility = ReservationAttachmentVisibilityEnum.Participants,
            ScanStatus = FileScanStatusEnum.NotScanned,

            UploadedBy = uploadedBy ?? reservation.UserId,

            CreatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(attachment);

        var context = services.GetRequiredService<AppDbContext>();

        await context.Set<ReservationAttachmentModel>()
            .AddAsync(attachment);

        await context.SaveChangesAsync();

        return attachment;
    }
}
