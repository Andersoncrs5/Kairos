using FluentAssertions;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomOperatingHour.Services;

public sealed class FindRoomOperatingHourServiceTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomOperatingHourRepository> _repositoryMock = new();

    private readonly FindRoomOperatingHourService _sut;

    public FindRoomOperatingHourServiceTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomOperatingHourRepository)
            .Returns(_repositoryMock.Object);

        _sut = new FindRoomOperatingHourService(
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenDtoIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();

        result.Errors.Should().Contain(
            "Room operating hour filter is required.");

        _repositoryMock.Verify(
            x => x.Find(
                It.IsAny<FilterRoomOperatingHourDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPage_WhenFilterIsValid()
    {
        // Arrange
        var dto = new FilterRoomOperatingHourDto
        {
            RoomId = Guid.NewGuid().ToString(),
            DayOfWeek = DayOfWeek.Monday,
            IsClosed = false,
            IsActive = true,
            Page = 1,
            PageSize = 20
        };

        var items = new List<RoomOperatingHourModel>
        {
            new()
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = dto.RoomId,
                DayOfWeek = DayOfWeek.Monday,
                OpensAt = new TimeOnly(8, 0),
                ClosesAt = new TimeOnly(12, 0),
                IsClosed = false,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new()
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = dto.RoomId,
                DayOfWeek = DayOfWeek.Monday,
                OpensAt = new TimeOnly(13, 0),
                ClosesAt = new TimeOnly(18, 0),
                IsClosed = false,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        };

        var expected = Page<RoomOperatingHourModel>.Create(
            items,
            totalCount: 2,
            pageNumber: 1,
            pageSize: 20);

        _repositoryMock
            .Setup(x => x.Find(
                dto,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expected);

        _repositoryMock.Verify(
            x => x.Find(
                dto,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyPage_WhenNoRecordsExist()
    {
        // Arrange
        var dto = new FilterRoomOperatingHourDto();

        var expected = Page<RoomOperatingHourModel>.Create(
            Array.Empty<RoomOperatingHourModel>(),
            totalCount: 0,
            pageNumber: 1,
            pageSize: 20);

        _repositoryMock
            .Setup(x => x.Find(
                dto,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepository()
    {
        // Arrange
        var dto = new FilterRoomOperatingHourDto();

        using var cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = cancellationTokenSource.Token;

        var expected = Page<RoomOperatingHourModel>.Create(
            Array.Empty<RoomOperatingHourModel>(),
            totalCount: 0,
            pageNumber: 1,
            pageSize: 20);

        _repositoryMock
            .Setup(x => x.Find(dto, cancellationToken))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(
            dto,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.Find(dto, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var dto = new FilterRoomOperatingHourDto();

        // Act
        var act = () => _sut.ExecuteAsync(
            dto,
            cancellationTokenSource.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _repositoryMock.Verify(
            x => x.Find(
                It.IsAny<FilterRoomOperatingHourDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}