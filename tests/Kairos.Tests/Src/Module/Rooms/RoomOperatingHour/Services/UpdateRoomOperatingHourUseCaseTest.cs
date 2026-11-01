using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomOperatingHour.Services;

public sealed class UpdateRoomOperatingHourUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomOperatingHourRepository> _repositoryMock = new();

    private readonly UpdateRoomOperatingHourUseCase _sut;

    public UpdateRoomOperatingHourUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomOperatingHourRepository)
            .Returns(_repositoryMock.Object);

        _sut = new UpdateRoomOperatingHourUseCase(
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

        VerifyNoUpdate();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenIdIsEmpty(
        string id)
    {
        // Arrange
        var command = CreateCommand(roomOperatingHourId: id);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Room operating hour ID is required.");

        VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenDtoIsNull()
    {
        // Arrange
        var command = new UpdateRoomOperatingHourCommand(
            Guid.NewGuid().ToString(),
            null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Room operating hour update data is required.");

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Entity retrieval
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRecordDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var command = CreateCommand(id);

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomOperatingHourModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain(
            "Room operating hour not found.");

        _repositoryMock.Verify(
            x => x.GetByRoomIdAndDayAsync(
                It.IsAny<string>(),
                It.IsAny<DayOfWeek>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoUpdate();
    }

    // ---------------------------------------------------------------
    // DTO validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenDayOfWeekIsInvalid()
    {
        // Arrange
        var model = CreateModel();

        SetupExistingModel(model);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                DayOfWeek = (DayOfWeek)7
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Invalid day of week.");

        VerifyNoUpdate();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenNotesExceedMaximumLength()
    {
        // Arrange
        var model = CreateModel();

        SetupExistingModel(model);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                Notes = new string('A', 501)
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Room operating hour notes cannot exceed 500 characters.");

        VerifyNoUpdate();
    }

    // ---------------------------------------------------------------
    // Opening and closing time
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenOpeningEqualsClosingTime()
    {
        // Arrange
        var model = CreateModel();

        SetupExistingModel(model);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(8, 0),
                ClosesAt = new TimeOnly(8, 0)
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Opening time must be earlier than closing time.");

        VerifyNoUpdate();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenOpeningIsAfterClosingTime()
    {
        // Arrange
        var model = CreateModel();

        SetupExistingModel(model);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(18, 0),
                ClosesAt = new TimeOnly(8, 0)
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Opening time must be earlier than closing time.");

        VerifyNoUpdate();
    }

    // ---------------------------------------------------------------
    // Closed day validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenClosingDayHasOtherActiveHours()
    {
        // Arrange
        var model = CreateModel();

        var otherHour = CreateExistingHour(
            model.RoomId,
            model.DayOfWeek,
            new TimeOnly(13, 0),
            new TimeOnly(18, 0));

        SetupExistingModel(model, otherHour);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                IsClosed = true
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Room already has operating hours configured for this day.");

        VerifyNoUpdate();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenDayIsAlreadyClosed()
    {
        // Arrange
        var model = CreateModel();

        var closedHour = CreateExistingHour(
            model.RoomId,
            model.DayOfWeek,
            TimeOnly.MinValue,
            TimeOnly.MinValue,
            isClosed: true);

        SetupExistingModel(model, closedHour);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(8, 0),
                ClosesAt = new TimeOnly(12, 0),
                IsClosed = false
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Room is marked as closed for this day.");

        VerifyNoUpdate();
    }

    // ---------------------------------------------------------------
    // Overlapping periods
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenOperatingHoursOverlap()
    {
        // Arrange
        var model = CreateModel();

        var otherHour = CreateExistingHour(
            model.RoomId,
            model.DayOfWeek,
            new TimeOnly(13, 0),
            new TimeOnly(18, 0));

        SetupExistingModel(model, otherHour);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(17, 0),
                ClosesAt = new TimeOnly(20, 0)
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Room operating hours overlap with an existing period.");

        VerifyNoUpdate();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowAdjacentOperatingHours_WhenPeriodsDoNotOverlap()
    {
        // Arrange
        var model = CreateModel();

        var otherHour = CreateExistingHour(
            model.RoomId,
            model.DayOfWeek,
            new TimeOnly(13, 0),
            new TimeOnly(18, 0));

        SetupExistingModel(model, otherHour);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(8, 0),
                ClosesAt = new TimeOnly(13, 0)
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OpensAt.Should().Be(new TimeOnly(8, 0));
        result.Value.ClosesAt.Should().Be(new TimeOnly(13, 0));

        _repositoryMock.Verify(
            x => x.Update(model),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreCurrentRecord_WhenCheckingOverlappingHours()
    {
        // Arrange
        var model = CreateModel();

        SetupExistingModel(model, model);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(13, 0)
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OpensAt.Should().Be(new TimeOnly(9, 0));
        result.Value.ClosesAt.Should().Be(new TimeOnly(13, 0));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreInactiveHours_WhenCheckingOverlaps()
    {
        // Arrange
        var model = CreateModel();

        var inactiveHour = CreateExistingHour(
            model.RoomId,
            model.DayOfWeek,
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            isActive: false);

        SetupExistingModel(model, inactiveHour);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(13, 0)
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Partial update
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateOnlyProvidedProperties()
    {
        // Arrange
        var model = CreateModel(
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0),
            version: 2);

        SetupExistingModel(model);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(13, 0)
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value.Should().BeSameAs(model);
        result.Value!.RoomId.Should().Be(model.RoomId);
        result.Value.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.Value.OpensAt.Should().Be(new TimeOnly(9, 0));
        result.Value.ClosesAt.Should().Be(new TimeOnly(13, 0));
        result.Value.IsClosed.Should().BeFalse();
        result.Value.IsActive.Should().BeTrue();

        result.Value.Version.Should().Be(3);
        result.Value.UpdatedAt.Should().NotBeNull();

        _repositoryMock.Verify(
            x => x.Update(model),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncrementVersion_WhenUpdatingRecord()
    {
        // Arrange
        var model = CreateModel(version: 5);

        SetupExistingModel(model);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                IsActive = false
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be(6);
        result.Value.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeactivateRecord_WhenIsActiveIsFalse()
    {
        // Arrange
        var model = CreateModel();

        SetupExistingModel(model);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                IsActive = false
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowOverlappingPeriod_WhenUpdatedRecordIsInactive()
    {
        // Arrange
        var model = CreateModel();

        var otherHour = CreateExistingHour(
            model.RoomId,
            model.DayOfWeek,
            new TimeOnly(9, 0),
            new TimeOnly(13, 0));

        SetupExistingModel(model, otherHour);

        var command = CreateCommand(
            model.Id,
            new UpdateRoomOperatingHourDto
            {
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(13, 0),
                IsActive = false
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeFalse();

        _repositoryMock.Verify(
            x => x.Update(model),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // ID normalization and cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldTrimId_BeforeRetrievingRecord()
    {
        // Arrange
        var model = CreateModel();

        SetupExistingModel(model);

        var command = CreateCommand(
            $"  {model.Id}  ");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                model.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepositories()
    {
        // Arrange
        var model = CreateModel();

        using var cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = cancellationTokenSource.Token;

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                model.Id,
                cancellationToken))
            .ReturnsAsync(model);

        _repositoryMock
            .Setup(x => x.GetByRoomIdAndDayAsync(
                model.RoomId,
                model.DayOfWeek,
                cancellationToken))
            .ReturnsAsync(
                (IReadOnlyList<RoomOperatingHourModel>)Array.Empty<RoomOperatingHourModel>());

        var command = CreateCommand(model.Id);

        // Act
        var result = await _sut.ExecuteAsync(
            command,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                model.Id,
                cancellationToken),
            Times.Once);

        _repositoryMock.Verify(
            x => x.GetByRoomIdAndDayAsync(
                model.RoomId,
                model.DayOfWeek,
                cancellationToken),
            Times.Once);

        _repositoryMock.Verify(
            x => x.Update(model),
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
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static UpdateRoomOperatingHourCommand CreateCommand(
        string? roomOperatingHourId = null,
        UpdateRoomOperatingHourDto? dto = null)
    {
        return new UpdateRoomOperatingHourCommand(
            roomOperatingHourId ?? Guid.NewGuid().ToString(),
            dto ?? new UpdateRoomOperatingHourDto());
    }

    private static RoomOperatingHourModel CreateModel(
        string? id = null,
        string? roomId = null,
        DayOfWeek dayOfWeek = DayOfWeek.Monday,
        TimeOnly? opensAt = null,
        TimeOnly? closesAt = null,
        bool isClosed = false,
        bool isActive = true,
        int version = 1)
    {
        return new RoomOperatingHourModel
        {
            Id = id ?? Guid.NewGuid().ToString(),
            RoomId = roomId ?? Guid.NewGuid().ToString(),
            DayOfWeek = dayOfWeek,
            OpensAt = opensAt ?? new TimeOnly(8, 0),
            ClosesAt = closesAt ?? new TimeOnly(12, 0),
            IsClosed = isClosed,
            IsActive = isActive,
            Version = version,
            CreatedAt = DateTimeOffset.UtcNow
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
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private void SetupExistingModel(
        RoomOperatingHourModel model,
        params RoomOperatingHourModel[] otherHours)
    {
        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                model.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        _repositoryMock
            .Setup(x => x.GetByRoomIdAndDayAsync(
                model.RoomId,
                model.DayOfWeek,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (IReadOnlyList<RoomOperatingHourModel>)otherHours);
    }

    private void VerifyNoUpdate()
    {
        _repositoryMock.Verify(
            x => x.Update(It.IsAny<RoomOperatingHourModel>()),
            Times.Never);
    }

    private void VerifyNoRepositoryCalls()
    {
        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _repositoryMock.Verify(
            x => x.GetByRoomIdAndDayAsync(
                It.IsAny<string>(),
                It.IsAny<DayOfWeek>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoUpdate();
    }
}