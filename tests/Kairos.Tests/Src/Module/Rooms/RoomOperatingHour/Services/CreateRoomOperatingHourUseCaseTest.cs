using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Rooms.RoomOperatingHour.Services;

public sealed class CreateRoomOperatingHourUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomRepository> _roomRepositoryMock = new();
    private readonly Mock<IRoomOperatingHourRepository> _repositoryMock = new();

    private readonly CreateRoomOperatingHourUseCase _sut;

    public CreateRoomOperatingHourUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomRepository)
            .Returns(_roomRepositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(x => x.RoomOperatingHourRepository)
            .Returns(_repositoryMock.Object);

        _repositoryMock
            .Setup(x => x.AddAsync(
                It.IsAny<RoomOperatingHourModel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new CreateRoomOperatingHourUseCase(
            _unitOfWorkMock.Object);
    }

    // ---------------------------------------------------------------
    // Command validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Room operating hour command is required.");

        VerifyNoPersistence();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsEmpty(
        string roomId)
    {
        // Arrange
        var command = CreateCommand(roomId);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Room ID is required.");

        VerifyNoPersistence();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenDtoIsNull()
    {
        // Arrange
        var command = new CreateRoomOperatingHourCommand(
            Guid.NewGuid().ToString(),
            null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Room operating hour data is required.");

        VerifyNoPersistence();
    }

    // ---------------------------------------------------------------
    // DTO validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenDayOfWeekIsInvalid()
    {
        // Arrange
        var dto = CreateDto();
        dto = new CreateRoomOperatingHourDto
        {
            DayOfWeek = (DayOfWeek)7,
            OpensAt = dto.OpensAt,
            ClosesAt = dto.ClosesAt,
            IsClosed = dto.IsClosed,
            Notes = dto.Notes
        };

        var command = CreateCommand(dto: dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Invalid day of week.");

        VerifyNoPersistence();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenNotesExceedMaximumLength()
    {
        // Arrange
        var dto = CreateDto();
        dto = new CreateRoomOperatingHourDto
        {
            DayOfWeek = dto.DayOfWeek,
            OpensAt = dto.OpensAt,
            ClosesAt = dto.ClosesAt,
            IsClosed = dto.IsClosed,
            Notes = new string('A', 501)
        };

        var command = CreateCommand(dto: dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Room operating hour notes cannot exceed 500 characters.");

        VerifyNoPersistence();
    }

    // ---------------------------------------------------------------
    // Opening and closing time
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenOpeningEqualsClosingTime()
    {
        // Arrange
        var dto = CreateDto(
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(8, 0));

        var command = CreateCommand(dto: dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Opening time must be earlier than closing time.");

        VerifyNoPersistence();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenOpeningIsAfterClosingTime()
    {
        // Arrange
        var dto = CreateDto(
            opensAt: new TimeOnly(18, 0),
            closesAt: new TimeOnly(8, 0));

        var command = CreateCommand(dto: dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Opening time must be earlier than closing time.");

        VerifyNoPersistence();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowEqualTimes_WhenRoomIsClosed()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);
        SetupExistingHours(roomId, DayOfWeek.Sunday);

        var dto = CreateDto(
            dayOfWeek: DayOfWeek.Sunday,
            opensAt: TimeOnly.MinValue,
            closesAt: TimeOnly.MinValue,
            isClosed: true);

        var command = CreateCommand(roomId, dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsClosed.Should().BeTrue();
        result.Value.OpensAt.Should().Be(TimeOnly.MinValue);
        result.Value.ClosesAt.Should().Be(TimeOnly.MinValue);

        _repositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<RoomOperatingHourModel>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Room existence
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, false);

        var command = CreateCommand(roomId);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room doesn't exist.");

        _repositoryMock.Verify(
            x => x.GetByRoomIdAndDayAsync(
                It.IsAny<string>(),
                It.IsAny<DayOfWeek>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoPersistence();
    }

    // ---------------------------------------------------------------
    // Existing operating hours
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenClosedDayAlreadyHasOperatingHours()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);

        SetupExistingHours(
            roomId,
            DayOfWeek.Monday,
            CreateExistingHour(
                roomId,
                DayOfWeek.Monday,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0)));

        var command = CreateCommand(
            roomId,
            CreateDto(
                dayOfWeek: DayOfWeek.Monday,
                isClosed: true));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Room already has operating hours configured for this day.");

        VerifyNoPersistence();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenDayIsAlreadyMarkedAsClosed()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);

        SetupExistingHours(
            roomId,
            DayOfWeek.Sunday,
            CreateExistingHour(
                roomId,
                DayOfWeek.Sunday,
                TimeOnly.MinValue,
                TimeOnly.MinValue,
                isClosed: true));

        var command = CreateCommand(
            roomId,
            CreateDto(
                dayOfWeek: DayOfWeek.Sunday));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Room is marked as closed for this day.");

        VerifyNoPersistence();
    }

    // ---------------------------------------------------------------
    // Overlapping periods
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenOperatingHoursOverlap()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);

        SetupExistingHours(
            roomId,
            DayOfWeek.Monday,
            CreateExistingHour(
                roomId,
                DayOfWeek.Monday,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0)));

        var command = CreateCommand(
            roomId,
            CreateDto(
                dayOfWeek: DayOfWeek.Monday,
                opensAt: new TimeOnly(10, 0),
                closesAt: new TimeOnly(14, 0)));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Room operating hours overlap with an existing period.");

        VerifyNoPersistence();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowAdjacentOperatingHours_WhenPeriodsDoNotOverlap()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);

        SetupExistingHours(
            roomId,
            DayOfWeek.Monday,
            CreateExistingHour(
                roomId,
                DayOfWeek.Monday,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0)));

        var command = CreateCommand(
            roomId,
            CreateDto(
                dayOfWeek: DayOfWeek.Monday,
                opensAt: new TimeOnly(12, 0),
                closesAt: new TimeOnly(18, 0)));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OpensAt.Should().Be(new TimeOnly(12, 0));
        result.Value.ClosesAt.Should().Be(new TimeOnly(18, 0));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreInactiveOperatingHours_WhenCheckingOverlaps()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);

        SetupExistingHours(
            roomId,
            DayOfWeek.Monday,
            CreateExistingHour(
                roomId,
                DayOfWeek.Monday,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0),
                isActive: false));

        var command = CreateCommand(
            roomId,
            CreateDto(
                dayOfWeek: DayOfWeek.Monday,
                opensAt: new TimeOnly(9, 0),
                closesAt: new TimeOnly(13, 0)));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    // ---------------------------------------------------------------
    // Creation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldCreateOperatingHour_WhenDataIsValid()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);
        SetupExistingHours(roomId, DayOfWeek.Monday);

        var dto = CreateDto(
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0),
            notes: "Morning operating hours.");

        var command = CreateCommand(roomId, dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);

        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(result.Value.Id, out _).Should().BeTrue();

        result.Value.RoomId.Should().Be(roomId);
        result.Value.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.Value.OpensAt.Should().Be(new TimeOnly(8, 0));
        result.Value.ClosesAt.Should().Be(new TimeOnly(12, 0));
        result.Value.IsClosed.Should().BeFalse();
        result.Value.IsActive.Should().BeTrue();
        result.Value.Notes.Should().Be("Morning operating hours.");
        result.Value.Version.Should().Be(1);
        result.Value.CreatedAt.Should().NotBe(default);

        _repositoryMock.Verify(
            x => x.AddAsync(
                It.Is<RoomOperatingHourModel>(model =>
                    model.RoomId == roomId &&
                    model.DayOfWeek == DayOfWeek.Monday &&
                    model.OpensAt == new TimeOnly(8, 0) &&
                    model.ClosesAt == new TimeOnly(12, 0) &&
                    model.IsActive &&
                    model.Version == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTrimRoomId_BeforeCheckingExistence()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);
        SetupExistingHours(roomId, DayOfWeek.Monday);

        var command = CreateCommand(
            $"  {roomId}  ");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.RoomId.Should().Be(roomId);

        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepositories()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId, true);
        SetupExistingHours(roomId, DayOfWeek.Monday);

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var command = CreateCommand(roomId);

        // Act
        var result = await _sut.ExecuteAsync(
            command,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(roomId, cancellationToken),
            Times.Once);

        _repositoryMock.Verify(
            x => x.GetByRoomIdAndDayAsync(
                roomId,
                DayOfWeek.Monday,
                cancellationToken),
            Times.Once);

        _repositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<RoomOperatingHourModel>(),
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var command = CreateCommand();

        // Act
        var act = () => _sut.ExecuteAsync(
            command,
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoPersistence();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static CreateRoomOperatingHourCommand CreateCommand(
        string? roomId = null,
        CreateRoomOperatingHourDto? dto = null)
    {
        return new CreateRoomOperatingHourCommand(
            roomId ?? Guid.NewGuid().ToString(),
            dto ?? CreateDto());
    }

    private static CreateRoomOperatingHourDto CreateDto(
        DayOfWeek dayOfWeek = DayOfWeek.Monday,
        TimeOnly? opensAt = null,
        TimeOnly? closesAt = null,
        bool isClosed = false,
        string? notes = null)
    {
        return new CreateRoomOperatingHourDto
        {
            DayOfWeek = dayOfWeek,
            OpensAt = opensAt ?? new TimeOnly(8, 0),
            ClosesAt = closesAt ?? new TimeOnly(12, 0),
            IsClosed = isClosed,
            Notes = notes
        };
    }

    private static RoomOperatingHourModel CreateExistingHour(
        string roomId,
        DayOfWeek dayOfWeek,
        TimeOnly opensAt,
        TimeOnly closesAt,
        bool isClosed = false,
        bool isActive = true)
    {
        return new RoomOperatingHourModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            DayOfWeek = dayOfWeek,
            OpensAt = opensAt,
            ClosesAt = closesAt,
            IsClosed = isClosed,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private void SetupRoomExists(string roomId, bool exists)
    {
        _roomRepositoryMock
            .Setup(x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);
    }

    private void SetupExistingHours(
        string roomId,
        DayOfWeek dayOfWeek,
        params RoomOperatingHourModel[] hours)
    {
        _repositoryMock
            .Setup(x => x.GetByRoomIdAndDayAsync(
                roomId,
                dayOfWeek,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoomOperatingHourModel>)hours);
    }

    private void VerifyNoPersistence()
    {
        _repositoryMock.Verify(
            x => x.AddAsync(
                It.IsAny<RoomOperatingHourModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}