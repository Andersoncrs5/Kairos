using FluentAssertions;

using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;

using NSubstitute;

namespace Kairos.Tests.Module.Reservations.ReservationAttachment.Services;

public sealed class DeleteReservationAttachmentUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReservationAttachmentRepository _attachmentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly DeleteReservationAttachmentUseCase _sut;

    public DeleteReservationAttachmentUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _attachmentRepository =
            Substitute.For<IReservationAttachmentRepository>();
        _fileStorageService =
            Substitute.For<IFileStorageService>();

        _unitOfWork
            .ReservationAttachmentRepository
            .Returns(_attachmentRepository);

        _unitOfWork
            .FileStorageService
            .Returns(_fileStorageService);

        _sut = new DeleteReservationAttachmentUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string attachmentId = "attachment-123";
        const string requesterUserId = "user-123";

        DeleteReservationAttachmentCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns((ReservationAttachmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("The reservation was not found.");

        await _fileStorageService
            .DidNotReceive()
            .DeleteAsync(
                Arg.Any<StorageBucket>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _attachmentRepository
            .DidNotReceive()
            .DeleteByIdAndCountAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenStorageDeletionFails_ReturnsFailure()
    {
        // Arrange
        const string attachmentId = "attachment-123";
        const string requesterUserId = "user-123";
        const string storageKey = "reservations/attachments/file.pdf";

        const int expectedStatusCode = 502;

        ReservationAttachmentModel attachment = new()
        {
            Id = attachmentId,
            ReservationId = "reservation-123",
            StorageKey = storageKey,
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            DisplayName = "Reservation document",
            CreatedAt = DateTimeOffset.UtcNow
        };

        DeleteReservationAttachmentCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns(attachment);

        _fileStorageService
            .DeleteAsync(
                StorageBucket.SupportMaterials,
                storageKey,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(
                "The file storage is temporarily unavailable.",
                expectedStatusCode));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(expectedStatusCode);

        result.Errors.Should().Contain(
            "The file storage is temporarily unavailable.");

        await _fileStorageService
            .Received(1)
            .DeleteAsync(
                StorageBucket.SupportMaterials,
                storageKey,
                Arg.Any<CancellationToken>());

        await _attachmentRepository
            .DidNotReceive()
            .DeleteByIdAndCountAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentExists_DeletesAttachment()
    {
        // Arrange
        const string attachmentId = "attachment-123";
        const string requesterUserId = "user-123";
        const string storageKey = "reservations/attachments/file.pdf";

        ReservationAttachmentModel attachment = new()
        {
            Id = attachmentId,
            ReservationId = "reservation-123",
            StorageKey = storageKey,
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            DisplayName = "Reservation document",
            CreatedAt = DateTimeOffset.UtcNow
        };

        DeleteReservationAttachmentCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns(attachment);

        _fileStorageService
            .DeleteAsync(
                StorageBucket.SupportMaterials,
                storageKey,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        _attachmentRepository
            .DeleteByIdAndCountAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        await _fileStorageService
            .Received(1)
            .DeleteAsync(
                StorageBucket.SupportMaterials,
                storageKey,
                Arg.Any<CancellationToken>());

        await _attachmentRepository
            .Received(1)
            .DeleteByIdAndCountAsync(
                attachmentId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_PropagatesCancellationToken()
    {
        // Arrange
        const string attachmentId = "attachment-123";
        const string requesterUserId = "user-123";
        const string storageKey = "reservations/attachments/file.pdf";

        using CancellationTokenSource cts = new();
        CancellationToken cancellationToken = cts.Token;

        ReservationAttachmentModel attachment = new()
        {
            Id = attachmentId,
            ReservationId = "reservation-123",
            StorageKey = storageKey,
            OriginalFileName = "file.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            DisplayName = "Reservation document",
            CreatedAt = DateTimeOffset.UtcNow
        };

        DeleteReservationAttachmentCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdAsync(attachmentId, cancellationToken)
            .Returns(attachment);

        _fileStorageService
            .DeleteAsync(
                StorageBucket.SupportMaterials,
                storageKey,
                cancellationToken)
            .Returns(Result<bool>.Success(true));

        _attachmentRepository
            .DeleteByIdAndCountAsync(
                attachmentId,
                cancellationToken)
            .Returns(Task.FromResult(1));

        // Act
        var result = await _sut.ExecuteAsync(
            command,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _attachmentRepository
            .Received(1)
            .GetByIdAsync(
                attachmentId,
                cancellationToken);

        await _fileStorageService
            .Received(1)
            .DeleteAsync(
                StorageBucket.SupportMaterials,
                storageKey,
                cancellationToken);

        await _attachmentRepository
            .Received(1)
            .DeleteByIdAndCountAsync(
                attachmentId,
                cancellationToken);
    }
}
