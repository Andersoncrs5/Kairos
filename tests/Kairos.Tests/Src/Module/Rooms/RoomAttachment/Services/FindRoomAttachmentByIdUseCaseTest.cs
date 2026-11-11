using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Repositories;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;

using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomAttachment.Services;

public sealed class FindRoomAttachmentByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoomAttachmentRepository _attachmentRepository;

    private readonly FindRoomAttachmentByIdUseCase _sut;

    public FindRoomAttachmentByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _attachmentRepository =
            Substitute.For<IRoomAttachmentRepository>();

        _unitOfWork
            .RoomAttachmentRepository
            .Returns(_attachmentRepository);

        _sut = new FindRoomAttachmentByIdUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string attachmentId = "room-attachment-123";
        const string requesterUserId = "user-123";

        FindRoomAttachmentByIdCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdWithDetailsAsync(
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

        await _attachmentRepository
            .Received(1)
            .GetByIdWithDetailsAsync(
                attachmentId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttachmentExists_ReturnsAttachment()
    {
        // Arrange
        const string attachmentId = "room-attachment-123";
        const string requesterUserId = "user-123";

        RoomAttachmentModel attachment = CreateAttachment(
            attachmentId);

        FindRoomAttachmentByIdCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdWithDetailsAsync(
                attachmentId,
                Arg.Any<CancellationToken>())
            .Returns(attachment);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();

        result.Value.Should().BeSameAs(attachment);

        result.Value!.Id.Should().Be(attachmentId);

        result.Value.RoomId.Should().Be("room-123");

        result.Value.StorageKey.Should().Be(
            "rooms/attachments/photo.jpg");

        result.Value.OriginalFileName.Should().Be(
            "photo.jpg");

        result.Value.ContentType.Should().Be(
            "image/jpeg");

        result.Value.Type.Should().Be(
            RoomAttachmentTypeEnum.Photo);

        await _attachmentRepository
            .Received(1)
            .GetByIdWithDetailsAsync(
                attachmentId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        FindRoomAttachmentByIdCommand command = null!;

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<ArgumentNullException>();

        await _attachmentRepository
            .DidNotReceive()
            .GetByIdWithDetailsAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_PropagatesCancellation()
    {
        // Arrange
        const string attachmentId = "room-attachment-123";
        const string requesterUserId = "user-123";

        using CancellationTokenSource cts = new();

        cts.Cancel();

        CancellationToken cancellationToken = cts.Token;

        FindRoomAttachmentByIdCommand command = new(
            attachmentId,
            requesterUserId);

        _attachmentRepository
            .GetByIdWithDetailsAsync(
                attachmentId,
                cancellationToken)
            .Returns(Task.FromCanceled<RoomAttachmentModel?>(
                cancellationToken));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(
            command,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _attachmentRepository
            .Received(1)
            .GetByIdWithDetailsAsync(
                attachmentId,
                cancellationToken);
    }

    private static RoomAttachmentModel CreateAttachment(
        string attachmentId)
    {
        return new RoomAttachmentModel
        {
            Id = attachmentId,
            RoomId = "room-123",
            Type = RoomAttachmentTypeEnum.Photo,
            StorageKey = "rooms/attachments/photo.jpg",
            OriginalFileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            AltText = "Room photo",
            SortOrder = 0,
            IsCover = true,
            CreatedBy = "user-123",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}