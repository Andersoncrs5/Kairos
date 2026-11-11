using System.Text;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Kairos.Tests.Module.Reservations.ReservationAttachment.Services;

public class CreateReservationAttachmentUseCaseTest
{
    private const string ReservationId = "reservation-123";
    private const string RequesterUserId = "user-123";

    private const string StorageKey =
        "reservations/reservation-123/attachments/document.pdf";

    private const string Sha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string FileContent = "PDF test content";

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReservationRepository> _reservationRepositoryMock;
    private readonly Mock<IReservationAttachmentRepository> _attachmentRepositoryMock;
    private readonly Mock<IFileStorageService> _storageMock;

    private readonly CreateReservationAttachmentUseCase _sut;

    private readonly StoredFile _storedFile;

    public CreateReservationAttachmentUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _reservationRepositoryMock =
            new Mock<IReservationRepository>();

        _attachmentRepositoryMock =
            new Mock<IReservationAttachmentRepository>();

        _storageMock =
            new Mock<IFileStorageService>();

        _storedFile = new StoredFile(
            StorageBucket.SupportMaterials,
            StorageKey,
            "application/pdf",
            Encoding.UTF8.GetByteCount(FileContent),
            Sha256,
            "etag-test");

        _unitOfWorkMock
            .SetupGet(x => x.ReservationRepository)
            .Returns(_reservationRepositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(x => x.ReservationAttachmentRepository)
            .Returns(_attachmentRepositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(x => x.FileStorageService)
            .Returns(_storageMock.Object);

        _reservationRepositoryMock
            .Setup(x => x.ExistsAsync(
                ReservationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _attachmentRepositoryMock
            .Setup(x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _storageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StoredFile>.Success(_storedFile));

        _sut = new CreateReservationAttachmentUseCase(
            _unitOfWorkMock.Object,
            NullLogger<CreateReservationAttachmentUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentIsCreated_ReturnsCreated()
    {
        // Arrange
        var command = CreateCommand();
        var ct = CancellationToken.None;

        ReservationAttachmentModel? persistedAttachment = null;
        UploadFileRequest? uploadRequest = null;

        _attachmentRepositoryMock
            .Setup(x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                ct))
            .Callback<ReservationAttachmentModel, CancellationToken>(
                (attachment, _) => persistedAttachment = attachment)
            .Returns(Task.CompletedTask);

        _storageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                ct))
            .Callback<UploadFileRequest, CancellationToken>(
                (request, _) => uploadRequest = request)
            .ReturnsAsync(Result<StoredFile>.Success(_storedFile));

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Value);

        Assert.NotNull(persistedAttachment);
        Assert.NotNull(uploadRequest);

        Assert.Equal(ReservationId, persistedAttachment!.ReservationId);
        Assert.Equal(StorageKey, persistedAttachment.StorageKey);
        Assert.Equal(Sha256, persistedAttachment.Sha256);
        Assert.Equal(RequesterUserId, persistedAttachment.UploadedBy);

        Assert.Equal("document.pdf", persistedAttachment.OriginalFileName);
        Assert.Equal("application/pdf", persistedAttachment.ContentType);
        Assert.Equal("Project attachment", persistedAttachment.DisplayName);
        Assert.Equal(
            ReservationAttachmentVisibilityEnum.Participants,
            persistedAttachment.Visibility);

        Assert.Equal(UploadKind.SupportMaterial, uploadRequest!.Kind);

        Assert.Equal(
            $"reservations/{ReservationId}/attachments",
            uploadRequest.KeyPrefix);

        _reservationRepositoryMock.Verify(
            x => x.ExistsAsync(ReservationId, ct),
            Times.Once);

        _storageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                ct),
            Times.Once);

        _attachmentRepositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                ct),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsValidationFailure()
    {
        // Arrange
        CreateReservationAttachmentCommand command = null!;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            "Reservation attachment command is required.",
            result.Errors);

        _reservationRepositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _storageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _attachmentRepositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = CreateCommand(reservationId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);
        Assert.NotEmpty(result.Errors);

        _reservationRepositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _storageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = CreateCommand(fileContent: Array.Empty<byte>());

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        Assert.Contains(
            "Reservation attachment file cannot be empty.",
            result.Errors);

        _reservationRepositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _storageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = CreateCommand();

        _reservationRepositoryMock
            .Setup(x => x.ExistsAsync(
                ReservationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(404, result.StatusCode);

        Assert.Contains(
            "The reservation was not found.",
            result.Errors);

        _storageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _attachmentRepositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUploadFails_ReturnsStorageFailure()
    {
        // Arrange
        var command = CreateCommand();
        const string errorMessage = "Storage upload failed.";

        _storageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<StoredFile>.Failure(
                    errorMessage,
                    503));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(503, result.StatusCode);
        Assert.Contains(errorMessage, result.Errors);

        _attachmentRepositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStorageReturnsEmptyFile_ReturnsFailure()
    {
        // Arrange
        var command = CreateCommand();

        _storageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<StoredFile>.Success(null!));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(502, result.StatusCode);

        Assert.Contains(
            "File storage returned an empty upload result.",
            result.Errors);

        _attachmentRepositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesException()
    {
        // Arrange
        var command = CreateCommand();

        _attachmentRepositoryMock
            .Setup(x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Database persistence failed."));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            act);

        Assert.Equal(
            "Database persistence failed.",
            exception.Message);

        _storageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();

        var command = CreateCommand();

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);

        _reservationRepositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _storageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<UploadFileRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _attachmentRepositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<ReservationAttachmentModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CreateReservationAttachmentCommand CreateCommand(
        string reservationId = ReservationId,
        string requesterUserId = RequesterUserId,
        string displayName = "Project attachment",
        byte[]? fileContent = null)
    {
        byte[] content = fileContent ??
            Encoding.UTF8.GetBytes(FileContent);

        var stream = new MemoryStream(content);

        var file = new FormFile(
            stream,
            0,
            content.Length,
            "File",
            "document.pdf")
        {
            Headers = new HeaderDictionary
            {
                ["Content-Type"] = "application/pdf"
            }
        };

        var dto = new CreateReservationAttachmentDto
        {
            File = file,
            DisplayName = displayName,
            Description = "Reservation supporting document",
            Visibility = ReservationAttachmentVisibilityEnum.Participants
        };

        return new CreateReservationAttachmentCommand(
            reservationId,
            requesterUserId,
            dto);
    }
}
