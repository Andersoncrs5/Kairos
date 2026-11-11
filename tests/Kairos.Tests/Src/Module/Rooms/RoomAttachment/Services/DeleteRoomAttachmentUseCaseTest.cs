using FluentAssertions;

using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Repositories;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;

using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomAttachment.Services;

public sealed class DeleteRoomAttachmentUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoomAttachmentRepository _attachmentRepository;
    private readonly IFileStorageService _fileStorageService;

    private readonly DeleteRoomAttachmentUseCase _sut;

    public DeleteRoomAttachmentUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _attachmentRepository = Substitute.For<IRoomAttachmentRepository>();
        _fileStorageService = Substitute.For<IFileStorageService>();

        _unitOfWork
            .RoomAttachmentRepository
            .Returns(_attachmentRepository);

        _unitOfWork
            .FileStorageService
            .Returns(_fileStorageService);

        _sut = new DeleteRoomAttachmentUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string attachmentId = "room-attachment-123";
        const string requesterUserId = "user-123";

        DeleteRoomAttachmentCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns((RoomAttachmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "The room attachment was not found.");

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
        const string attachmentId = "room-attachment-123";
        const string requesterUserId = "user-123";
        const string storageKey = "rooms/attachments/file.jpg";
        const int expectedStatusCode = 502;

        RoomAttachmentModel attachment = new()
        {
            Id = attachmentId,
            RoomId = "room-123",
            StorageKey = storageKey,
            OriginalFileName = "file.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            Type = RoomAttachmentTypeEnum.Photo,
            AltText = "Room photo",
            SortOrder = 0,
            IsCover = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DeleteRoomAttachmentCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns(attachment);

        _fileStorageService
            .DeleteAsync(
                StorageBucket.RoomMedia,
                storageKey,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<bool>.Failure(
                    "The file storage is temporarily unavailable.",
                    expectedStatusCode)));

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
                StorageBucket.RoomMedia,
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
        const string attachmentId = "room-attachment-123";
        const string requesterUserId = "user-123";
        const string storageKey = "rooms/attachments/file.jpg";

        RoomAttachmentModel attachment = new()
        {
            Id = attachmentId,
            RoomId = "room-123",
            StorageKey = storageKey,
            OriginalFileName = "file.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            Type = RoomAttachmentTypeEnum.Photo,
            AltText = "Room photo",
            SortOrder = 0,
            IsCover = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DeleteRoomAttachmentCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns(attachment);

        _fileStorageService
            .DeleteAsync(
                StorageBucket.RoomMedia,
                storageKey,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<bool>.Success(true)));

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
                StorageBucket.RoomMedia,
                storageKey,
                Arg.Any<CancellationToken>());

        await _attachmentRepository
            .Received(1)
            .DeleteByIdAndCountAsync(
                attachmentId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationTokenIsProvided_PropagatesIt()
    {
        // Arrange
        const string attachmentId = "room-attachment-123";
        const string requesterUserId = "user-123";
        const string storageKey = "rooms/attachments/file.jpg";

        using CancellationTokenSource cts = new();

        CancellationToken cancellationToken = cts.Token;

        RoomAttachmentModel attachment = new()
        {
            Id = attachmentId,
            RoomId = "room-123",
            StorageKey = storageKey,
            OriginalFileName = "file.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            Type = RoomAttachmentTypeEnum.Photo,
            AltText = "Room photo",
            SortOrder = 0,
            IsCover = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DeleteRoomAttachmentCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdAsync(
                attachmentId,
                cancellationToken)
            .Returns(attachment);

        _fileStorageService
            .DeleteAsync(
                StorageBucket.RoomMedia,
                storageKey,
                cancellationToken)
            .Returns(Task.FromResult(
                Result<bool>.Success(true)));

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
                StorageBucket.RoomMedia,
                storageKey,
                cancellationToken);

        await _attachmentRepository
            .Received(1)
            .DeleteByIdAndCountAsync(
                attachmentId,
                cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        DeleteRoomAttachmentCommand command = null!;

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<ArgumentNullException>();

        await _attachmentRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _fileStorageService
            .DidNotReceive()
            .DeleteAsync(
                Arg.Any<StorageBucket>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }
}