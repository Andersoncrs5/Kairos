using System.Text;
using Amazon.S3.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Repositories;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Rooms.RoomAttachment.Services;

public class DownloadRoomAttachmentByIdUseCaseTest
{
    private const string AttachmentId = "room-attachment-123";
    private const string StorageKey = "rooms/room-123/attachment-123.jpg";

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRoomAttachmentRepository> _repositoryMock;
    private readonly Mock<IFileStorageService> _storageMock;

    private readonly DownloadRoomAttachmentByIdUseCase _sut;

    public DownloadRoomAttachmentByIdUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _repositoryMock = new Mock<IRoomAttachmentRepository>();
        _storageMock = new Mock<IFileStorageService>();

        _unitOfWorkMock
            .SetupGet(x => x.RoomAttachmentRepository)
            .Returns(_repositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(x => x.FileStorageService)
            .Returns(_storageMock.Object);

        _sut = new DownloadRoomAttachmentByIdUseCase(
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
            .ReturnsAsync((RoomAttachmentModel?)null);

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

        using var fileDownload = CreateFileDownload();

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                AttachmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        _storageMock
            .Setup(x => x.DownloadAsync(
                StorageBucket.RoomMedia,
                StorageKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<FileDownload>.Success(fileDownload));

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(fileDownload, result.Value);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                AttachmentId,
                ct),
            Times.Once);

        _storageMock.Verify(
            x => x.DownloadAsync(
                StorageBucket.RoomMedia,
                StorageKey,
                ct),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        DownloadRoomAttachmentByIdCommand command = null!;

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

    [Fact]
    public async Task ExecuteAsync_WhenStorageFails_ReturnsStorageFailure()
    {
        // Arrange
        var command = CreateCommand();
        var ct = CancellationToken.None;

        var attachment = CreateAttachment();

        var storageFailure = Result<FileDownload>.Failure(
            ["Error downloading room attachment."],
            500);

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                AttachmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        _storageMock
            .Setup(x => x.DownloadAsync(
                StorageBucket.RoomMedia,
                StorageKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storageFailure);

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(
            storageFailure.StatusCode,
            result.StatusCode);

        Assert.Equal(
            storageFailure.Errors,
            result.Errors);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                AttachmentId,
                ct),
            Times.Once);

        _storageMock.Verify(
            x => x.DownloadAsync(
                StorageBucket.RoomMedia,
                StorageKey,
                ct),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalled_PropagatesCancellationToken()
    {
        // Arrange
        var command = CreateCommand();

        using var cts = new CancellationTokenSource();
        CancellationToken ct = cts.Token;

        var attachment = CreateAttachment();

        using var fileDownload = CreateFileDownload();

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                AttachmentId,
                ct))
            .ReturnsAsync(attachment);

        _storageMock
            .Setup(x => x.DownloadAsync(
                StorageBucket.RoomMedia,
                StorageKey,
                ct))
            .ReturnsAsync(
                Result<FileDownload>.Success(fileDownload));

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
                StorageBucket.RoomMedia,
                StorageKey,
                ct),
            Times.Once);
    }

    private static DownloadRoomAttachmentByIdCommand CreateCommand()
    {
        return new DownloadRoomAttachmentByIdCommand(
            AttachmentId,
            Guid.NewGuid().ToString());
    }

    private static RoomAttachmentModel CreateAttachment()
    {
        return new RoomAttachmentModel
        {
            Id = AttachmentId,
            RoomId = "room-123",
            StorageKey = StorageKey,
            OriginalFileName = "room-photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            Sha256 = "sha256-test",
            Type = RoomAttachmentTypeEnum.Photo,
            AltText = "Room photo",
            SortOrder = 0,
            IsCover = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static FileDownload CreateFileDownload()
    {
        var content = new MemoryStream(
            Encoding.UTF8.GetBytes("Room attachment test content"));

        var response = new GetObjectResponse
        {
            ResponseStream = content,
            ContentLength = content.Length,
            LastModified = DateTime.UtcNow,
            ETag = "etag-test"
        };

        response.Headers.ContentType = "image/jpeg";
        response.Headers.ContentLength = content.Length;

        return new FileDownload(response);
    }
}