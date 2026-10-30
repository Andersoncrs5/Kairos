using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;
using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomBlockout.Service;

public sealed class UpdateRoomBlockoutUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomBlockoutRepository _roomBlockoutRepository;
    private readonly UpdateRoomBlockoutUseCase _sut;

    private readonly DateTimeOffset _createdAt =
        DateTimeOffset.UtcNow.AddDays(-1);

    private readonly DateTimeOffset _startsAt =
        DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(9);

    private readonly DateTimeOffset _endsAt =
        DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(11);

    public UpdateRoomBlockoutUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _roomRepository = Substitute.For<IRoomRepository>();
        _roomBlockoutRepository = Substitute.For<IRoomBlockoutRepository>();

        _unitOfWork.RoomRepository.Returns(_roomRepository);
        _unitOfWork.RoomBlockoutRepository.Returns(_roomBlockoutRepository);

        _sut = new UpdateRoomBlockoutUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // Validação do command
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsValidationFailure()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout update command is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room blockout ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new UpdateRoomBlockoutCommand(
            Guid.NewGuid().ToString(),
            null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout update data is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand("invalid-uuid");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            Guid.NewGuid().ToString(),
            NewDto(roomId: "   "));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room ID cannot be empty.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            Guid.NewGuid().ToString(),
            NewDto(roomId: "invalid-uuid"));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReasonContainsOnlyWhitespace_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            Guid.NewGuid().ToString(),
            NewDto(reason: "   "));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout reason cannot be empty.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTypeIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            Guid.NewGuid().ToString(),
            NewDto(type: (RoomBlockoutType)999));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartDateIsDefault_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            Guid.NewGuid().ToString(),
            NewDto(startsAt: DateTimeOffset.MinValue));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout start date is invalid.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenEndDateIsDefault_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            Guid.NewGuid().ToString(),
            NewDto(endsAt: DateTimeOffset.MinValue));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout end date is invalid.");

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Bloqueio inexistente ou cancelado
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenBlockoutDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var command = NewCommand(id);

        _roomBlockoutRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomBlockoutModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room blockout not found.");

        await _roomBlockoutRepository
            .Received(1)
            .GetByIdAsync(id, Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenBlockoutIsCancelled_ReturnsConflict()
    {
        // Arrange
        var blockout = CreateBlockout(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            RoomBlockoutStatus.Cancelled);

        SetupExistingBlockout(blockout);

        // Act
        var result = await _sut.ExecuteAsync(NewCommand(blockout.Id));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Cancelled room blockouts cannot be updated.");

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    // ---------------------------------------------------------------
    // Validação do período
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenStartDateIsAfterExistingEndDate_ReturnsValidationFailure()
    {
        // Arrange
        var blockout = CreateBlockout(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());

        SetupExistingBlockout(blockout);

        var command = NewCommand(
            blockout.Id,
            NewDto(startsAt: blockout.EndsAt.AddMinutes(1)));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout start date must be earlier than end date.");

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartDateEqualsEndDate_ReturnsValidationFailure()
    {
        // Arrange
        var blockout = CreateBlockout(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());

        SetupExistingBlockout(blockout);

        var command = NewCommand(
            blockout.Id,
            NewDto(endsAt: blockout.StartsAt));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout start date must be earlier than end date.");

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    // ---------------------------------------------------------------
    // Troca de sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenNewRoomDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var currentRoomId = Guid.NewGuid().ToString();
        var newRoomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), currentRoomId);

        SetupExistingBlockout(blockout);

        _roomRepository
            .GetByIdAsync(newRoomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(null));

        var command = NewCommand(
            blockout.Id,
            NewDto(roomId: newRoomId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room doesn't exist.");

        await _roomRepository
            .Received(1)
            .GetByIdAsync(newRoomId, Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewRoomIsInactive_ReturnsBadRequest()
    {
        // Arrange
        var currentRoomId = Guid.NewGuid().ToString();
        var newRoomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), currentRoomId);
        var newRoom = CreateRoom(newRoomId, isActive: false);

        SetupExistingBlockout(blockout);

        _roomRepository
            .GetByIdAsync(newRoomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(newRoom));

        var command = NewCommand(
            blockout.Id,
            NewDto(roomId: newRoomId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room is not active.");

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomChangesToActiveRoom_UpdatesRoomReference()
    {
        // Arrange
        var currentRoomId = Guid.NewGuid().ToString();
        var newRoomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), currentRoomId);
        var newRoom = CreateRoom(newRoomId);

        SetupExistingBlockout(blockout);
        SetupRoom(newRoom);
        SetupNoOverlaps(newRoomId, blockout.StartsAt, blockout.EndsAt);

        var command = NewCommand(
            blockout.Id,
            NewDto(roomId: newRoomId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(blockout);
        blockout.RoomId.Should().Be(newRoomId);
        blockout.Room.Should().BeSameAs(newRoom);

        _roomBlockoutRepository
            .Received(1)
            .Update(blockout);
    }

    // ---------------------------------------------------------------
    // Conflitos de horário
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAnotherActiveBlockoutOverlaps_ReturnsConflict()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), roomId);
        var otherBlockout = CreateBlockout(Guid.NewGuid().ToString(), roomId);

        SetupExistingBlockout(blockout);

        _roomBlockoutRepository
            .GetOverlappingAsync(
                roomId,
                blockout.StartsAt,
                blockout.EndsAt,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RoomBlockoutModel>>(
                [otherBlockout]));

        // Act
        var result = await _sut.ExecuteAsync(NewCommand(blockout.Id));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Room blockout overlaps an existing blockout.");

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyTheCurrentBlockoutOverlaps_DoesNotReturnConflict()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), roomId);

        SetupExistingBlockout(blockout);

        _roomBlockoutRepository
            .GetOverlappingAsync(
                roomId,
                blockout.StartsAt,
                blockout.EndsAt,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RoomBlockoutModel>>(
                [blockout]));

        // Act
        var result = await _sut.ExecuteAsync(NewCommand(blockout.Id));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(blockout);

        _roomBlockoutRepository
            .Received(1)
            .Update(blockout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyCancelledBlockoutsOverlap_DoesNotReturnConflict()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), roomId);
        var cancelledBlockout = CreateBlockout(
            Guid.NewGuid().ToString(),
            roomId,
            RoomBlockoutStatus.Cancelled);

        SetupExistingBlockout(blockout);

        _roomBlockoutRepository
            .GetOverlappingAsync(
                roomId,
                blockout.StartsAt,
                blockout.EndsAt,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RoomBlockoutModel>>(
                [cancelledBlockout]));

        // Act
        var result = await _sut.ExecuteAsync(NewCommand(blockout.Id));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _roomBlockoutRepository
            .Received(1)
            .Update(blockout);
    }

    // ---------------------------------------------------------------
    // Atualização bem-sucedida e parcial
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_UpdatesRoomBlockout()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), roomId);

        var newStartsAt = blockout.StartsAt.AddHours(1);
        var newEndsAt = blockout.EndsAt.AddHours(1);

        SetupExistingBlockout(blockout);
        SetupNoOverlaps(roomId, newStartsAt, newEndsAt);

        var command = NewCommand(
            blockout.Id,
            NewDto(
                type: RoomBlockoutType.Cleaning,
                reason: "  Updated reason  ",
                notes: "  Updated notes  ",
                startsAt: newStartsAt,
                endsAt: newEndsAt));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(blockout);

        blockout.RoomId.Should().Be(roomId);
        blockout.Type.Should().Be(RoomBlockoutType.Cleaning);
        blockout.Reason.Should().Be("Updated reason");
        blockout.Notes.Should().Be("Updated notes");
        blockout.StartsAt.Should().Be(newStartsAt);
        blockout.EndsAt.Should().Be(newEndsAt);

        // O caso de uso não altera implicitamente os campos de auditoria.
        blockout.Version.Should().Be(3);
        blockout.CreatedAt.Should().Be(_createdAt);
        blockout.UpdatedAt.Should().BeNull();

        await _roomBlockoutRepository
            .Received(1)
            .GetByIdAsync(blockout.Id, Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .Received(1)
            .GetOverlappingAsync(
                roomId,
                newStartsAt,
                newEndsAt,
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .Received(1)
            .Update(blockout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoFieldsAreNull_PreservesExistingValues()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), roomId);

        SetupExistingBlockout(blockout);
        SetupNoOverlaps(roomId, blockout.StartsAt, blockout.EndsAt);

        var originalType = blockout.Type;
        var originalReason = blockout.Reason;
        var originalNotes = blockout.Notes;
        var originalStartsAt = blockout.StartsAt;
        var originalEndsAt = blockout.EndsAt;

        // DTO vazio representa uma atualização parcial sem campos alterados.
        var command = NewCommand(blockout.Id, new UpdateRoomBlockoutDto());

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        blockout.RoomId.Should().Be(roomId);
        blockout.Type.Should().Be(originalType);
        blockout.Reason.Should().Be(originalReason);
        blockout.Notes.Should().Be(originalNotes);
        blockout.StartsAt.Should().Be(originalStartsAt);
        blockout.EndsAt.Should().Be(originalEndsAt);

        _roomRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .Received(1)
            .Update(blockout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotesContainOnlyWhitespace_ClearsNotes()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), roomId);

        SetupExistingBlockout(blockout);
        SetupNoOverlaps(roomId, blockout.StartsAt, blockout.EndsAt);

        var command = NewCommand(
            blockout.Id,
            NewDto(notes: "   "));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        blockout.Notes.Should().BeNull();

        _roomBlockoutRepository
            .Received(1)
            .Update(blockout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdContainsBracesAndWhitespace_NormalizesRoomId()
    {
        // Arrange
        var roomGuid = Guid.NewGuid();
        var roomId = roomGuid.ToString();
        var blockout = CreateBlockout(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        var room = CreateRoom(roomId);

        SetupExistingBlockout(blockout);
        SetupRoom(room);
        SetupNoOverlaps(roomId, blockout.StartsAt, blockout.EndsAt);

        var command = NewCommand(
            blockout.Id,
            NewDto(roomId: $" {roomGuid:B} "));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        blockout.RoomId.Should().Be(roomId);
        blockout.Room.Should().BeSameAs(room);
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var command = NewCommand(Guid.NewGuid().ToString());

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static UpdateRoomBlockoutCommand NewCommand(
        string id,
        UpdateRoomBlockoutDto? dto = null)
    {
        return new UpdateRoomBlockoutCommand(
            id,
            dto ?? NewDto());
    }

    private static UpdateRoomBlockoutDto NewDto(
        string? roomId = null,
        RoomBlockoutType? type = null,
        string? reason = null,
        string? notes = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        return new UpdateRoomBlockoutDto
        {
            RoomId = roomId,
            Type = type,
            Reason = reason,
            Notes = notes,
            StartsAt = startsAt,
            EndsAt = endsAt
        };
    }

    private RoomBlockoutModel CreateBlockout(
        string id,
        string roomId,
        RoomBlockoutStatus status = RoomBlockoutStatus.Scheduled)
    {
        return new RoomBlockoutModel
        {
            Id = id,
            Version = 3,
            CreatedAt = _createdAt,
            UpdatedAt = null,
            RoomId = roomId,
            Type = RoomBlockoutType.Maintenance,
            Status = status,
            Reason = "Existing maintenance",
            Notes = "Existing notes",
            StartsAt = _startsAt,
            EndsAt = _endsAt,
            CreatedBy = Guid.NewGuid().ToString()
        };
    }

    private static RoomModel CreateRoom(
        string roomId,
        bool isActive = true)
    {
        return new RoomModel
        {
            Id = roomId,
            RoomTypeId = Guid.NewGuid().ToString(),
            Code = $"R-{Guid.NewGuid():N}"[..10],
            Name = "Integration Test Room",
            Location = "Building A",
            Capacity = 10,
            IsAccessible = false,
            IsReservable = true,
            IsActive = isActive,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private void SetupExistingBlockout(RoomBlockoutModel blockout)
    {
        _roomBlockoutRepository
            .GetByIdAsync(blockout.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomBlockoutModel?>(blockout));
    }

    private void SetupRoom(RoomModel room)
    {
        _roomRepository
            .GetByIdAsync(room.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));
    }

    private void SetupNoOverlaps(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        _roomBlockoutRepository
            .GetOverlappingAsync(
                roomId,
                startsAt,
                endsAt,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RoomBlockoutModel>>(
                Array.Empty<RoomBlockoutModel>()));
    }

    private async Task VerifyNoRepositoryInteractions()
    {
        await _roomBlockoutRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _roomRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }
}
