
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

public sealed class FindReservationAttachmentByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReservationAttachmentRepository _attachmentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly FindReservationAttachmentByIdUseCase _sut;

    public FindReservationAttachmentByIdUseCaseTest()
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

        _sut = new FindReservationAttachmentByIdUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string attachmentId = "attachment-123";
        const string requesterUserId = "user-123";

        FindReservationAttachmentByIdCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdWithDetailsAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns((ReservationAttachmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "The reservation attachment was not found.");

        _fileStorageService
            .DidNotReceive()
            .GetDownloadUrl(
                Arg.Any<StorageBucket>(),
                Arg.Any<string>(),
                Arg.Any<DownloadUrlOptions?>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDownloadUrlGenerationFails_ReturnsFailure()
    {
        // Arrange
        const string attachmentId = "attachment-123";
        const string requesterUserId = "user-123";
        const string storageKey = "reservations/attachments/file.pdf";

        const int expectedStatusCode = 502;

        ReservationAttachmentModel attachment = CreateAttachment(
            attachmentId,
            storageKey);

        FindReservationAttachmentByIdCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdWithDetailsAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns(attachment);

        _fileStorageService
            .GetDownloadUrl(
                StorageBucket.SupportMaterials,
                storageKey,
                null)
            .Returns(Result<PresignedUrl>.Failure(
                "The file storage is temporarily unavailable.",
                expectedStatusCode));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(expectedStatusCode);

        result.Errors.Should().Contain(
            "The file storage is temporarily unavailable.");

        _fileStorageService
            .Received(1)
            .GetDownloadUrl(
                StorageBucket.SupportMaterials,
                storageKey,
                null);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentExists_ReturnsAttachmentWithDownloadUrl()
    {
        // Arrange
        const string attachmentId = "attachment-123";
        const string requesterUserId = "user-123";
        const string storageKey = "reservations/attachments/file.pdf";
        const string downloadUrl =
            "http://localhost:9000/kairos-support-materials/file.pdf";

        DateTimeOffset expiration = DateTimeOffset.UtcNow.AddMinutes(15);

        ReservationAttachmentModel attachment = CreateAttachment(
            attachmentId,
            storageKey);

        FindReservationAttachmentByIdCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdWithDetailsAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns(attachment);

        _fileStorageService
            .GetDownloadUrl(
                StorageBucket.SupportMaterials,
                storageKey,
                null)
            .Returns(Result<PresignedUrl>.Success(
                new PresignedUrl(downloadUrl, expiration)));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(attachmentId);
        result.Value.DisplayName.Should().Be("Reservation document");
        result.Value.DownloadUrl.Should().Be(downloadUrl);

        await _attachmentRepository
            .Received(1)
            .GetByIdWithDetailsAsync(
                attachmentId,
                Arg.Any<CancellationToken>());

        _fileStorageService
            .Received(1)
            .GetDownloadUrl(
                StorageBucket.SupportMaterials,
                storageKey,
                null);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_PropagatesCancellation()
    {
        // Arrange
        const string attachmentId = "attachment-123";
        const string requesterUserId = "user-123";

        using CancellationTokenSource cts = new();
        cts.Cancel();

        CancellationToken cancellationToken = cts.Token;

        FindReservationAttachmentByIdCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdWithDetailsAsync(
                attachmentId,
                cancellationToken)
            .Returns(Task.FromCanceled<ReservationAttachmentModel?>(
                cancellationToken));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(
            command,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _fileStorageService
            .DidNotReceive()
            .GetDownloadUrl(
                Arg.Any<StorageBucket>(),
                Arg.Any<string>(),
                Arg.Any<DownloadUrlOptions?>());
    }

    private static ReservationAttachmentModel CreateAttachment(
        string attachmentId,
        string storageKey)
    {
        return new ReservationAttachmentModel
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
    }
}
