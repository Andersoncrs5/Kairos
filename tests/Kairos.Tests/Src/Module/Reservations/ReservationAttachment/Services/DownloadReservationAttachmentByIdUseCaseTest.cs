using System.Text;
using Amazon.S3.Model;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Reservations.ReservationAttachment.Services;

public class DownloadReservationAttachmentByIdUseCaseTest
{
    private const string AttachmentId = "attachment-123";
    private const string StorageKey = "reservations/attachment-123.pdf";

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReservationAttachmentRepository> _repositoryMock;
    private readonly Mock<IFileStorageService> _storageMock;

    private readonly DownloadReservationAttachmentByIdUseCase _sut;

    public DownloadReservationAttachmentByIdUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _repositoryMock = new Mock<IReservationAttachmentRepository>();
        _storageMock = new Mock<IFileStorageService>();

        _unitOfWorkMock
            .SetupGet(x => x.ReservationAttachmentRepository)
            .Returns(_repositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(x => x.FileStorageService)
            .Returns(_storageMock.Object);

        _sut = new DownloadReservationAttachmentByIdUseCase(
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = CreateCommand();
        var ct = CancellationToken.None;

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                AttachmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationAttachmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        Assert.False(result.IsSuccess);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                AttachmentId,
                ct),
            Times.Once);

        _storageMock.Verify(
            x => x.DownloadAsync(
                It.IsAny<StorageBucket>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentExists_DownloadsFileSuccessfully()
    {
        // Arrange
        var command = CreateCommand();
        var ct = CancellationToken.None;

        var attachment = CreateAttachment();

        var fileDownload = CreateFileDownload();

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                AttachmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        _storageMock
            .Setup(x => x.DownloadAsync(
                StorageBucket.SupportMaterials,
                StorageKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FileDownload>.Success(fileDownload));

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        Assert.True(result.IsSuccess);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                AttachmentId,
                ct),
            Times.Once);

        _storageMock.Verify(
            x => x.DownloadAsync(
                StorageBucket.SupportMaterials,
                StorageKey,
                ct),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        DownloadReservationAttachmentByIdCommand command = null!;

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _storageMock.Verify(
            x => x.DownloadAsync(
                It.IsAny<StorageBucket>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DownloadReservationAttachmentByIdCommand CreateCommand()
    {
        return new DownloadReservationAttachmentByIdCommand(AttachmentId, Guid.NewGuid().ToString());
    }

    private static ReservationAttachmentModel CreateAttachment()
    {
        return new ReservationAttachmentModel
        {
            Id = AttachmentId,
            ReservationId = "reservation-123",
            StorageKey = StorageKey,
            OriginalFileName = "document.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            Sha256 = "sha256-test",
            DisplayName = "Reservation document",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static FileDownload CreateFileDownload()
    {
        var content = new MemoryStream(
            Encoding.UTF8.GetBytes("PDF test content"));

        var response = new GetObjectResponse
        {
            ResponseStream = content,
            ContentLength = content.Length,
            LastModified = DateTime.UtcNow,
            ETag = "etag-test"
        };

        response.Headers.ContentType = "application/pdf";
        response.Headers.ContentLength = content.Length;

        return new FileDownload(response);
    }
}
