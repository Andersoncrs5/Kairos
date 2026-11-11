using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Repositories;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace Kairos.Tests.Module.Rooms.RoomAttachment.Services;

public sealed class UpdateRoomAttachmentUseCaseTest
{
    private const string AttachmentId =
        "7d8c4f62-5c31-4a7b-9f12-3e6a8b2c4d10";

    private const string RequesterUserId = "user-123";

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    private readonly Mock<IRoomAttachmentRepository>
        _attachmentRepositoryMock;

    private readonly UpdateRoomAttachmentUseCase _sut;

    public UpdateRoomAttachmentUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _attachmentRepositoryMock =
            new Mock<IRoomAttachmentRepository>();

        _unitOfWorkMock
            .SetupGet(x => x.RoomAttachmentRepository)
            .Returns(_attachmentRepositoryMock.Object);

        _sut = new UpdateRoomAttachmentUseCase(
            _unitOfWorkMock.Object,
            NullLogger<UpdateRoomAttachmentUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentIsUpdated_ReturnsUpdatedAttachment()
    {
        // Arrange
        var command = CreateCommand();
        var ct = CancellationToken.None;

        var attachment = CreateAttachment();

        _attachmentRepositoryMock
            .Setup(x => x.FindByIdWithForUpdateAsync(
                AttachmentId,
                It.IsAny<TimeSpan?>(),
                ct))
            .ReturnsAsync(attachment);

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);

        Assert.Equal(AttachmentId, result.Value.Id);
        Assert.Equal("Updated alt text", result.Value.AltText);
        Assert.Equal(2, result.Value.SortOrder);
        Assert.True(result.Value.IsCover);

        Assert.NotNull(result.Value.UpdatedAt);
        Assert.True(result.Value.UpdatedAt > attachment.CreatedAt);

        // Storage metadata must remain unchanged.
        Assert.Equal(
            "rooms/original/photo.jpg",
            result.Value.StorageKey);

        Assert.Equal(
            new string('a', 64),
            result.Value.Sha256);

        Assert.Equal(
            "photo.jpg",
            result.Value.OriginalFileName);

        Assert.Equal(
            "image/jpeg",
            result.Value.ContentType);

        Assert.Equal(
            RequesterUserId,
            result.Value.CreatedBy);

        Assert.Equal("room-123", result.Value.RoomId);

        Assert.Equal(
            RoomAttachmentTypeEnum.Photo,
            result.Value.Type);

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                AttachmentId,
                It.IsAny<TimeSpan?>(),
                ct),
            Times.Once);

        _attachmentRepositoryMock.Verify(
            x => x.Update(It.IsAny<RoomAttachmentModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsValidationFailure()
    {
        // Arrange
        UpdateRoomAttachmentCommand command = null!;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            "Room attachment command is required.",
            result.Errors);

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = CreateCommand(
            attachmentId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "Room attachment ID is required."));

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new UpdateRoomAttachmentCommand(
            AttachmentId,
            RequesterUserId,
            null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "Room attachment data is required."));

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = CreateCommand(
            altText: new string('a', 256));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);
        Assert.NotEmpty(result.Errors);

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSortOrderIsNegative_ReturnsValidationFailure()
    {
        // Arrange
        var command = CreateCommand(
            sortOrder: -1);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "Sort order cannot be negative."));

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentIdIsNotValidUuid_ReturnsValidationFailure()
    {
        // Arrange
        var command = CreateCommand(
            attachmentId: "invalid-uuid");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            "Room attachment ID must be a valid UUID.",
            result.Errors);

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = CreateCommand();
        var ct = CancellationToken.None;

        _attachmentRepositoryMock
            .Setup(x => x.FindByIdWithForUpdateAsync(
                AttachmentId,
                It.IsAny<TimeSpan?>(),
                ct))
            .ReturnsAsync((RoomAttachmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(404, result.StatusCode);

        Assert.Contains(
            "Room attachment not found.",
            result.Errors);

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                AttachmentId,
                It.IsAny<TimeSpan?>(),
                ct),
            Times.Once);

        _attachmentRepositoryMock.Verify(
            x => x.Update(It.IsAny<RoomAttachmentModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFloorPlanIsMarkedAsCover_ReturnsValidationFailure()
    {
        // Arrange
        var command = CreateCommand(
            isCover: true);

        var attachment = CreateAttachment(
            type: RoomAttachmentTypeEnum.FloorPlan);

        _attachmentRepositoryMock
            .Setup(x => x.FindByIdWithForUpdateAsync(
                AttachmentId,
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            "Only photo attachments can be marked as cover.",
            result.Errors);

        // The attachment must not be modified.
        Assert.Equal("Original alt text", attachment.AltText);
        Assert.Equal(0, attachment.SortOrder);
        Assert.False(attachment.IsCover);
        Assert.Null(attachment.UpdatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenIsCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        cts.Cancel();

        var command = CreateCommand();

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static UpdateRoomAttachmentCommand CreateCommand(
        string attachmentId = AttachmentId,
        string requesterUserId = RequesterUserId,
        string? altText = "Updated alt text",
        int sortOrder = 2,
        bool isCover = true)
    {
        var dto = new UpdateRoomAttachmentDto
        {
            AltText = altText,
            SortOrder = sortOrder,
            IsCover = isCover
        };

        return new UpdateRoomAttachmentCommand(
            attachmentId,
            requesterUserId,
            dto);
    }

    private static RoomAttachmentModel CreateAttachment(
        RoomAttachmentTypeEnum type = RoomAttachmentTypeEnum.Photo)
    {
        return new RoomAttachmentModel
        {
            Id = AttachmentId,
            RoomId = "room-123",
            Type = type,
            StorageKey = "rooms/original/photo.jpg",
            OriginalFileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            AltText = "Original alt text",
            SortOrder = 0,
            IsCover = false,
            CreatedBy = RequesterUserId,
            CreatedAt = new DateTimeOffset(
                2025, 1, 1, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = null,
            Version = 1
        };
    }
}