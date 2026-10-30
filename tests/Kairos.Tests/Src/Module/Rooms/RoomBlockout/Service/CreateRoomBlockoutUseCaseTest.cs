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
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomBlockout.Service;

public sealed class CreateRoomBlockoutUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IRoomRepository> _roomRepositoryMock = new();
    private readonly Mock<IRoomBlockoutRepository> _blockoutRepositoryMock = new();

    private readonly CreateRoomBlockoutUseCase _sut;

    public CreateRoomBlockoutUseCaseTest()
    {
        _uowMock
            .SetupGet(uow => uow.RoomRepository)
            .Returns(_roomRepositoryMock.Object);

        _uowMock
            .SetupGet(uow => uow.RoomBlockoutRepository)
            .Returns(_blockoutRepositoryMock.Object);

        _sut = new CreateRoomBlockoutUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_ShouldCreateRoomBlockout()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var createdBy = Guid.NewGuid().ToString();

        var now = DateTimeOffset.UtcNow;
        var startsAt = now.AddHours(2);
        var endsAt = now.AddHours(4);

        var dto = CreateValidDto(
            roomId: roomId,
            startsAt: startsAt,
            endsAt: endsAt,
            reason: "Scheduled maintenance",
            notes: "Air conditioning maintenance");

        var command = CreateCommand(dto, createdBy);

        var room = CreateRoom(roomId);

        SetupExistingRoom(room);
        SetupNoOverlappingBlockout(roomId, startsAt, endsAt);

        RoomBlockoutModel? addedBlockout = null;

        _blockoutRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<RoomBlockoutModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomBlockoutModel, CancellationToken>(
                (blockout, _) => addedBlockout = blockout)
            .Returns(Task.CompletedTask);

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(command);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        addedBlockout.Should().NotBeNull();

        var blockout = addedBlockout!;

        Guid.TryParse(blockout.Id, out _).Should().BeTrue();

        blockout.RoomId.Should().Be(roomId);
        blockout.Type.Should().Be(command.Dto.Type);
        blockout.Status.Should().Be(RoomBlockoutStatus.Scheduled);
        blockout.Reason.Should().Be("Scheduled maintenance");
        blockout.Notes.Should().Be("Air conditioning maintenance");
        blockout.StartsAt.Should().Be(startsAt);
        blockout.EndsAt.Should().Be(endsAt);
        blockout.CreatedBy.Should().Be(createdBy);

        blockout.Version.Should().Be(1);
        blockout.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        blockout.CreatedAt.Should().BeOnOrBefore(afterExecution);
        blockout.UpdatedAt.Should().BeNull();

        blockout.CancelledAt.Should().BeNull();
        blockout.CancelledBy.Should().BeNull();
        blockout.CancellationReason.Should().BeNull();

        result.Value.Should().BeSameAs(blockout);

        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _blockoutRepositoryMock.Verify(
            repository => repository.ExistsOverlappingAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _blockoutRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.Is<RoomBlockoutModel>(added =>
                    added.Id == blockout.Id &&
                    added.RoomId == roomId &&
                    added.CreatedBy == createdBy &&
                    added.Status == RoomBlockoutStatus.Scheduled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTextContainsWhitespace_ShouldNormalizeText()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var createdBy = Guid.NewGuid().ToString();

        var dto = CreateValidDto(
            roomId: $" {roomId:B} ",
            reason: "  Preventive maintenance  ",
            notes: "  Air conditioning inspection  ");

        var command = CreateCommand(dto, createdBy);

        var normalizedRoomId = roomId.ToString();
        var startsAt = dto.StartsAt;
        var endsAt = dto.EndsAt;

        SetupExistingRoom(CreateRoom(normalizedRoomId));
        SetupNoOverlappingBlockout(normalizedRoomId, startsAt, endsAt);

        RoomBlockoutModel? addedBlockout = null;

        _blockoutRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<RoomBlockoutModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomBlockoutModel, CancellationToken>(
                (blockout, _) => addedBlockout = blockout)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        addedBlockout.Should().NotBeNull();

        addedBlockout!.RoomId.Should().Be(normalizedRoomId);
        addedBlockout.Reason.Should().Be("Preventive maintenance");
        addedBlockout.Notes.Should().Be("Air conditioning inspection");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotesAreEmpty_ShouldNormalizeNotesToNull()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        var dto = CreateValidDto(
            roomId: roomId,
            notes: "   ");

        SetupExistingRoom(CreateRoom(roomId));
        SetupNoOverlappingBlockout(roomId, dto.StartsAt, dto.EndsAt);

        RoomBlockoutModel? addedBlockout = null;

        _blockoutRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<RoomBlockoutModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomBlockoutModel, CancellationToken>(
                (blockout, _) => addedBlockout = blockout)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsSuccess.Should().BeTrue();
        addedBlockout.Should().NotBeNull();
        addedBlockout!.Notes.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validação do command e do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ShouldReturnValidationFailure()
    {
        // Arrange
        CreateRoomBlockoutCommand command = null!;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout command is required.");

        VerifyRepositoriesWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ShouldReturnValidationFailure()
    {
        // Arrange
        var command = new CreateRoomBlockoutCommand
        {
            Dto = null!,
            CreatedBy = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        VerifyRepositoriesWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReasonExceedsMaximumLength_ShouldReturnValidationFailure()
    {
        // Arrange
        var dto = CreateValidDto(
            reason: new string('a', 256));

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout reason cannot exceed 255 characters.");

        VerifyRepositoriesWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotesExceedMaximumLength_ShouldReturnValidationFailure()
    {
        // Arrange
        var dto = CreateValidDto(
            notes: new string('a', 1001));

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout notes cannot exceed 1000 characters.");

        VerifyRepositoriesWereNotCalled();
    }

    // ---------------------------------------------------------------
    // Validação do criador
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCreatedByIsMissing_ShouldReturnValidationFailure()
    {
        // Arrange
        var command = CreateCommand(
            CreateValidDto(),
            createdBy: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout creator ID is required.");

        VerifyRepositoriesWereNotCalled();
    }

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task ExecuteAsync_WhenCreatedByIsInvalid_ShouldReturnValidationFailure(
        string createdBy)
    {
        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(CreateValidDto(), createdBy));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Created by must be a valid UUID.");

        VerifyRepositoriesWereNotCalled();
    }

    // ---------------------------------------------------------------
    // Validação da sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsEmpty_ShouldReturnValidationFailure()
    {
        // Arrange
        var dto = CreateValidDto(roomId: " ");

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        VerifyRepositoriesWereNotCalled();
    }

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task ExecuteAsync_WhenRoomIdIsInvalid_ShouldReturnValidationFailure(
        string roomId)
    {
        // Arrange
        var dto = CreateValidDto(roomId: roomId);

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room ID must be a valid UUID.");

        VerifyRepositoriesWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        var dto = CreateValidDto(roomId: roomId);

        _roomRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Room doesn't exist.");

        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _blockoutRepositoryMock.Verify(
            repository => repository.ExistsOverlappingAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIsInactive_ShouldReturnBadRequest()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        var dto = CreateValidDto(roomId: roomId);

        var room = CreateRoom(roomId, isActive: false);
        SetupExistingRoom(room);

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room is not active.");

        _blockoutRepositoryMock.Verify(
            repository => repository.ExistsOverlappingAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Validação do período
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenStartDateIsDefault_ShouldReturnValidationFailure()
    {
        // Arrange
        var dto = CreateValidDto(
            startsAt: DateTimeOffset.MinValue);

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout start and end dates are required.");

        VerifyRepositoriesWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenEndDateIsDefault_ShouldReturnValidationFailure()
    {
        // Arrange
        var dto = CreateValidDto(
            endsAt: DateTimeOffset.MinValue);

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout start and end dates are required.");

        VerifyRepositoriesWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartDateIsAfterEndDate_ShouldReturnValidationFailure()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        var dto = CreateValidDto(
            startsAt: now.AddHours(3),
            endsAt: now.AddHours(1));

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout start date must be earlier than end date.");

        VerifyRepositoriesWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartDateEqualsEndDate_ShouldReturnValidationFailure()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        var dto = CreateValidDto(
            startsAt: now,
            endsAt: now);

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout start date must be earlier than end date.");

        VerifyRepositoriesWereNotCalled();
    }

    // ---------------------------------------------------------------
    // Conflitos de horário
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenBlockoutOverlaps_ShouldReturnConflict()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        var dto = CreateValidDto(
            roomId: roomId);

        SetupExistingRoom(CreateRoom(roomId));

        _blockoutRepositoryMock
            .Setup(repository => repository.ExistsOverlappingAsync(
                roomId,
                dto.StartsAt,
                dto.EndsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(
            CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room blockout overlaps an existing blockout.");

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = CreateCommand(CreateValidDto());

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        VerifyRepositoriesWereNotCalled();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static CreateRoomBlockoutCommand CreateCommand(
        CreateRoomBlockoutDto dto)
    {
        return CreateCommand(dto, Guid.NewGuid().ToString());
    }

    private static CreateRoomBlockoutCommand CreateCommand(
        CreateRoomBlockoutDto dto,
        string? createdBy)
    {
        return new CreateRoomBlockoutCommand
        {
            Dto = dto,
            CreatedBy = createdBy
        };
    }

    private static CreateRoomBlockoutDto CreateValidDto(
        string? roomId = null,
        RoomBlockoutType type = RoomBlockoutType.Maintenance,
        string reason = "Scheduled maintenance",
        string? notes = "Preventive maintenance",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new CreateRoomBlockoutDto
        {
            RoomId = roomId ?? Guid.NewGuid().ToString(),
            Type = type,
            Reason = reason,
            Notes = notes,
            StartsAt = startsAt ?? now.AddHours(1),
            EndsAt = endsAt ?? now.AddHours(2)
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

    private void SetupExistingRoom(RoomModel room)
    {
        _roomRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                room.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);
    }

    private void SetupNoOverlappingBlockout(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        _blockoutRepositoryMock
            .Setup(repository => repository.ExistsOverlappingAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private void VerifyRepositoriesWereNotCalled()
    {
        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _blockoutRepositoryMock.Verify(
            repository => repository.ExistsOverlappingAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    private void VerifyAddAsyncWasNotCalled()
    {
        _blockoutRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<RoomBlockoutModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
