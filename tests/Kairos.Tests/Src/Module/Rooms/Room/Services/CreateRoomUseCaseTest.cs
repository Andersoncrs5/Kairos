using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.Room.Services.Providers;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Moq;

namespace Kairos.Tests.Module.Rooms.Room.Services;

public sealed class CreateRoomUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IRoomRepository> _roomRepositoryMock = new();
    private readonly Mock<IRoomTypeRepository> _roomTypeRepositoryMock = new();

    private readonly CreateRoomUseCase _sut;

    public CreateRoomUseCaseTest()
    {
        _uowMock
            .SetupGet(uow => uow.RoomRepository)
            .Returns(_roomRepositoryMock.Object);

        _uowMock
            .SetupGet(uow => uow.RoomTypeRepository)
            .Returns(_roomTypeRepositoryMock.Object);

        _sut = new CreateRoomUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ShouldReturnBadRequest()
    {
        // Arrange
        CreateRoomDto dto = null!;

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room data is required.");

        VerifyRoomTypeLookupWasNotCalled();
        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsInvalid_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = CreateValidDto(
            code: new string('A', 31));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room code cannot exceed 30 characters.");

        VerifyRoomTypeLookupWasNotCalled();
        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomTypeIdIsInvalidUuid_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = CreateValidDto(
            roomTypeId: "invalid-uuid");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room type ID must be a valid UUID.");

        VerifyRoomTypeLookupWasNotCalled();
        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeIsWhitespace_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = CreateValidDto(code: "   ");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room code is required.");

        VerifyRoomTypeLookupWasNotCalled();
        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameIsWhitespace_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = CreateValidDto(name: "   ");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        VerifyRoomTypeLookupWasNotCalled();
        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLocationIsWhitespace_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = CreateValidDto(location: "   ");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room location is required.");

        VerifyRoomTypeLookupWasNotCalled();
        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Verificação do tipo de sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomTypeDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var roomTypeId = Guid.NewGuid().ToString();

        var dto = CreateValidDto(
            roomTypeId: roomTypeId);

        _roomTypeRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                roomTypeId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomTypeModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Room type doesn't exist.");

        _roomTypeRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                roomTypeId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomTypeIsInactive_ShouldReturnBadRequest()
    {
        // Arrange
        var roomTypeId = Guid.NewGuid().ToString();

        var dto = CreateValidDto(
            roomTypeId: roomTypeId);

        SetupRoomType(roomTypeId, isActive: false);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room type is not active.");

        _roomTypeRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                roomTypeId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Unicidade do código
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCodeAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        var roomTypeId = Guid.NewGuid().ToString();

        var dto = CreateValidDto(
            roomTypeId: roomTypeId,
            code: "ROOM-01");

        SetupRoomType(roomTypeId);

        _roomRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                "ROOM-01",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room with the code 'ROOM-01' already exists.");

        _roomTypeRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                roomTypeId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _roomRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                "ROOM-01",
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Criação da sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_ShouldCreateRoom()
    {
        // Arrange
        var roomTypeGuid = Guid.NewGuid();
        var roomTypeId = roomTypeGuid.ToString();

        var dto = CreateValidDto(
            roomTypeId: $" {roomTypeGuid:B} ",
            code: "  ROOM-01  ",
            name: "  Meeting Room  ",
            description: "  Room for meetings  ",
            location: "  Building A  ",
            floor: "  Second floor  ",
            capacity: 12,
            areaSquareMeters: 25.50m,
            isAccessible: true,
            isReservable: false);

        SetupRoomType(roomTypeId);

        _roomRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                "ROOM-01",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        RoomModel? addedRoom = null;

        _roomRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<RoomModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomModel, CancellationToken>(
                (room, _) => addedRoom = room)
            .Returns(Task.CompletedTask);

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(dto);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        addedRoom.Should().NotBeNull();

        var room = addedRoom!;

        Guid.TryParse(room.Id, out _).Should().BeTrue();

        room.RoomTypeId.Should().Be(roomTypeId);
        room.Code.Should().Be("ROOM-01");
        room.Name.Should().Be("Meeting Room");
        room.Description.Should().Be("Room for meetings");
        room.Location.Should().Be("Building A");
        room.Floor.Should().Be("Second floor");
        room.Capacity.Should().Be(12);
        room.AreaSquareMeters.Should().Be(25.50m);
        room.IsAccessible.Should().BeTrue();
        room.IsReservable.Should().BeFalse();

        room.Version.Should().Be(1);
        room.IsActive.Should().BeTrue();
        room.UpdatedAt.Should().BeNull();

        room.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        room.CreatedAt.Should().BeOnOrBefore(afterExecution);

        room.Should().BeEquivalentTo(result.Value);

        _roomTypeRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                roomTypeId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _roomRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                "ROOM-01",
                It.IsAny<CancellationToken>()),
            Times.Once);

        _roomRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.Is<RoomModel>(added =>
                    added.Id == room.Id &&
                    added.RoomTypeId == roomTypeId &&
                    added.Code == "ROOM-01" &&
                    added.Name == "Meeting Room"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOptionalFieldsAreEmpty_ShouldNormalizeThemToNull()
    {
        // Arrange
        var roomTypeId = Guid.NewGuid().ToString();

        var dto = CreateValidDto(
            roomTypeId: roomTypeId,
            description: "   ",
            floor: "   ");

        SetupRoomType(roomTypeId);

        _roomRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                dto.Code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        RoomModel? addedRoom = null;

        _roomRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<RoomModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomModel, CancellationToken>(
                (room, _) => addedRoom = room)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        addedRoom.Should().NotBeNull();

        addedRoom!.Description.Should().BeNull();
        addedRoom.Floor.Should().BeNull();

        _roomRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<RoomModel>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var dto = CreateValidDto();

        // Act
        var act = () => _sut.ExecuteAsync(
            dto,
            cancellationTokenSource.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _roomTypeRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyCodeLookupWasNotCalled();
        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void SetupRoomType(
        string roomTypeId,
        bool isActive = true)
    {
        var roomType = new RoomTypeModel
        {
            Id = roomTypeId,
            Name = "Test Room Type",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _roomTypeRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                roomTypeId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(roomType);
    }

    private static CreateRoomDto CreateValidDto(
        string? roomTypeId = null,
        string code = "ROOM-01",
        string name = "Meeting Room",
        string? description = "Room for meetings",
        string location = "Building A",
        string? floor = "First floor",
        int capacity = 10,
        decimal? areaSquareMeters = 20m,
        bool isAccessible = false,
        bool isReservable = true)
    {
        return new CreateRoomDto
        {
            RoomTypeId = roomTypeId ?? Guid.NewGuid().ToString(),
            Code = code,
            Name = name,
            Description = description,
            Location = location,
            Floor = floor,
            Capacity = capacity,
            AreaSquareMeters = areaSquareMeters,
            IsAccessible = isAccessible,
            IsReservable = isReservable
        };
    }

    private void VerifyRoomTypeLookupWasNotCalled()
    {
        _roomTypeRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyCodeLookupWasNotCalled()
    {
        _roomRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyAddAsyncWasNotCalled()
    {
        _roomRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<RoomModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}