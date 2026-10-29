using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomType.Services;

public sealed class CreateRoomTypeUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IRoomTypeRepository> _repositoryMock = new();
    private readonly CreateRoomTypeUseCase _sut;

    public CreateRoomTypeUseCaseTest()
    {
        _uowMock
            .Setup(uow => uow.RoomTypeRepository)
            .Returns(_repositoryMock.Object);

        _repositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<RoomTypeModel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new CreateRoomTypeUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsRoomType_WhenDataIsValid()
    {
        // Arrange
        var dto = NewDto();

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                dto.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        RoomTypeModel? addedRoomType = null;

        _repositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<RoomTypeModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomTypeModel, CancellationToken>(
                (roomType, _) => addedRoomType = roomType)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().NotBeNull();

        result.Value.Name.Should().Be(dto.Name);
        result.Value.Description.Should().Be(dto.Description);
        result.Value.Id.Should().NotBeNullOrWhiteSpace();
        result.Value.Version.Should().Be(1);
        result.Value.IsActive.Should().BeTrue();
        result.Value.CreatedAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(5));

        addedRoomType.Should().BeSameAs(result.Value);

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                dto.Name,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<RoomTypeModel>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TrimsName_BeforeCheckingAndCreating()
    {
        // Arrange
        var dto = NewDto(name: "  Double  ");

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                "Double",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        RoomTypeModel? addedRoomType = null;

        _repositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<RoomTypeModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomTypeModel, CancellationToken>(
                (roomType, _) => addedRoomType = roomType)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeFalse();
        addedRoomType.Should().NotBeNull();
        addedRoomType!.Name.Should().Be("Double");

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                "Double",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        // Arrange
        var dto = NewDto();

        using var cts = new CancellationTokenSource();

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                dto.Name,
                cts.Token))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(dto, cts.Token);

        // Assert
        result.IsFailure.Should().BeFalse();

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                dto.Name,
                cts.Token),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var dto = NewDto(name: "");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        // Arrange
        var dto = NewDto(name: new string('a', 101));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenDescriptionExceedsMaximumLength()
    {
        // Arrange
        var dto = NewDto(description: new string('a', 256));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenNameAlreadyExists()
    {
        // Arrange
        var dto = NewDto(name: "Double");

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                "Double",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room type with the name 'Double' already exists.");

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var dto = NewDto();

        // Act
        var act = () => _sut.ExecuteAsync(dto, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static CreateRoomTypeDto NewDto(
        string name = "Double",
        string? description = "Double room")
    {
        return new CreateRoomTypeDto
        {
            Name = name,
            Description = description
        };
    }

    private void VerifyAddAsyncWasNotCalled()
    {
        _repositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<RoomTypeModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}