using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Kairos.Tests.Module.Reservations.ReservationAttachment.Services;

public class UpdateReservationAttachmentUseCaseTest
{
    private const string AttachmentId =
        "7d8c4f62-5c31-4a7b-9f12-3e6a8b2c4d10";

    private const string RequesterUserId =
        "user-123";

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReservationAttachmentRepository>
        _attachmentRepositoryMock;

    private readonly UpdateReservationAttachmentUseCase _sut;

    public UpdateReservationAttachmentUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _attachmentRepositoryMock =
            new Mock<IReservationAttachmentRepository>();

        _unitOfWorkMock
            .SetupGet(x => x.ReservationAttachmentRepository)
            .Returns(_attachmentRepositoryMock.Object);

        _sut = new UpdateReservationAttachmentUseCase(
            _unitOfWorkMock.Object,
            NullLogger<UpdateReservationAttachmentUseCase>.Instance);
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
        Assert.Equal("Updated attachment", result.Value.DisplayName);
        Assert.Equal("Updated description", result.Value.Description);

        Assert.Equal(
            ReservationAttachmentVisibilityEnum.OwnerOnly,
            result.Value.Visibility);

        Assert.NotNull(result.Value.UpdatedAt);
        Assert.True(result.Value.UpdatedAt > attachment.CreatedAt);

        // Storage metadata must remain unchanged.
        Assert.Equal("reservations/original/document.pdf", result.Value.StorageKey);
        Assert.Equal("original-sha256", result.Value.Sha256);
        Assert.Equal("document.pdf", result.Value.OriginalFileName);
        Assert.Equal("application/pdf", result.Value.ContentType);
        Assert.Equal(RequesterUserId, result.Value.UploadedBy);

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                AttachmentId,
                It.IsAny<TimeSpan?>(),
                ct),
            Times.Once);

        _attachmentRepositoryMock.Verify(
            x => x.Update(It.IsAny<ReservationAttachmentModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsValidationFailure()
    {
        // Arrange
        UpdateReservationAttachmentCommand command = null!;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            "Reservation attachment command is required.",
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
            error => error.Contains("Reservation attachment ID is required."));

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
        var command = new UpdateReservationAttachmentCommand(
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
                "Reservation attachment data is required."));

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
        var command = CreateCommand(displayName: string.Empty);

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
    public async Task ExecuteAsync_WhenVisibilityIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = CreateCommand(
            visibility: (ReservationAttachmentVisibilityEnum)999);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "Reservation attachment visibility is invalid."));

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
            "Reservation attachment ID must be a valid UUID.",
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
            .ReturnsAsync((ReservationAttachmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(404, result.StatusCode);

        Assert.Contains(
            "Reservation attachment not found.",
            result.Errors);

        _attachmentRepositoryMock.Verify(
            x => x.FindByIdWithForUpdateAsync(
                AttachmentId,
                It.IsAny<TimeSpan?>(),
                ct),
            Times.Once);

        _attachmentRepositoryMock.Verify(
            x => x.Update(It.IsAny<ReservationAttachmentModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDescriptionIsEmpty_NormalizesDescriptionToNull()
    {
        // Arrange
        var command = CreateCommand(
            description: "   ");

        var attachment = CreateAttachment();

        _attachmentRepositoryMock
            .Setup(x => x.FindByIdWithForUpdateAsync(
                AttachmentId,
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Description);
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

    private static UpdateReservationAttachmentCommand CreateCommand(
        string attachmentId = AttachmentId,
        string requesterUserId = RequesterUserId,
        string displayName = "Updated attachment",
        string? description = "Updated description",
        ReservationAttachmentVisibilityEnum visibility =
            ReservationAttachmentVisibilityEnum.OwnerOnly)
    {
        var dto = new UpdateReservationAttachmentDto
        {
            DisplayName = displayName,
            Description = description,
            Visibility = visibility
        };

        return new UpdateReservationAttachmentCommand(
            attachmentId,
            requesterUserId,
            dto);
    }

    private static ReservationAttachmentModel CreateAttachment()
    {
        return new ReservationAttachmentModel
        {
            Id = AttachmentId,
            ReservationId = "reservation-123",
            StorageKey = "reservations/original/document.pdf",
            OriginalFileName = "document.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            Sha256 = "original-sha256",
            DisplayName = "Original attachment",
            Description = "Original description",
            Visibility = ReservationAttachmentVisibilityEnum.Participants,
            UploadedBy = RequesterUserId,
            CreatedAt = new DateTimeOffset(
                2025, 1, 1, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = null,
            Version = 1
        };
    }
}
