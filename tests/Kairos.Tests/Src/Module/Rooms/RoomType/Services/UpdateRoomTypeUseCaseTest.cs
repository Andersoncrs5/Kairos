using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomType.Services;

public sealed class UpdateRoomTypeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoomTypeRepository _roomTypeRepository;
    private readonly UpdateRoomTypeUseCase _sut;

    public UpdateRoomTypeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _roomTypeRepository = Substitute.For<IRoomTypeRepository>();

        _unitOfWork.RoomTypeRepository.Returns(_roomTypeRepository);

        _sut = new UpdateRoomTypeUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // Validação do comando
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
            "Room type update command is required.");

        VerifyNoRepositoryInteractions();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        // Arrange
        var command = NewCommand(id);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room type ID is required.");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new UpdateRoomTypeCommand(
            Guid.NewGuid().ToString(),
            null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room type update data is required.");

        VerifyNoRepositoryInteractions();
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
            "Id must be a valid UUID.");

        VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenNameExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var dto = NewDto(
            name: new string('a', 101));

        var command = NewCommand(id, dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(error =>
            error.Contains("100", StringComparison.Ordinal));

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDescriptionExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var dto = NewDto(
            description: new string('a', 256));

        var command = NewCommand(id, dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(error =>
            error.Contains("255", StringComparison.Ordinal));

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameContainsOnlyWhitespace_ReturnsValidationFailure()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var command = NewCommand(
            id,
            NewDto(name: "   "));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room type name cannot be empty.");

        VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Tipo de quarto inexistente
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomTypeDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var command = NewCommand(
            id,
            NewDto(name: "Updated room type"));

        _roomTypeRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomTypeModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Room type not found.");

        await _roomTypeRepository
            .Received(1)
            .GetByIdAsync(id, Arg.Any<CancellationToken>());

        await _roomTypeRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _roomTypeRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomTypeModel>());
    }

    // ---------------------------------------------------------------
    // Conflito de nome
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenNameAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var roomType = CreateRoomType(
            id,
            name: "Old room type");

        var command = NewCommand(
            id,
            NewDto(name: "Existing room type"));

        _roomTypeRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomTypeModel?>(roomType));

        _roomTypeRepository
            .ExistsByNameAsync(
                "Existing room type",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room type with the name 'Existing room type' already exists.");

        roomType.Name.Should().Be("Old room type");
        roomType.Description.Should().Be("Existing description");
        roomType.IsActive.Should().BeTrue();

        _roomTypeRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomTypeModel>());
    }

    // ---------------------------------------------------------------
    // Atualização bem-sucedida
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_UpdatesRoomType()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();

        var ct = cts.Token;
        var roomType = CreateRoomType(id, "Old room type");

        var dto = NewDto(
            name: "  Updated room type  ",
            description: "  Updated description  ",
            isActive: false);

        var command = NewCommand(id, dto);

        _roomTypeRepository
            .GetByIdAsync(id, ct)
            .Returns(Task.FromResult<RoomTypeModel?>(roomType));

        _roomTypeRepository
            .ExistsByNameAsync("Updated room type", ct)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(roomType);

        roomType.Id.Should().Be(id);
        roomType.Name.Should().Be("Updated room type");
        roomType.Description.Should().Be("Updated description");
        roomType.IsActive.Should().BeFalse();

        // Os campos de auditoria existentes não devem ser alterados
        // implicitamente por este caso de uso.
        roomType.Version.Should().Be(3);
        roomType.CreatedAt.Should().Be(_createdAt);
        roomType.UpdatedAt.Should().BeNull();

        await _roomTypeRepository
            .Received(1)
            .GetByIdAsync(id, ct);

        await _roomTypeRepository
            .Received(1)
            .ExistsByNameAsync("Updated room type", ct);

        _roomTypeRepository
            .Received(1)
            .Update(roomType);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameIsUnchanged_DoesNotCheckDuplicate()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var roomType = CreateRoomType(id, "Double");

        var command = NewCommand(
            id,
            NewDto(
                name: "Double",
                description: null,
                isActive: null));

        _roomTypeRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomTypeModel?>(roomType));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(roomType);

        roomType.Name.Should().Be("Double");
        roomType.Description.Should().Be("Existing description");
        roomType.IsActive.Should().BeTrue();

        await _roomTypeRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _roomTypeRepository
            .Received(1)
            .Update(roomType);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameDiffersOnlyByCase_DoesNotCheckDuplicate()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var roomType = CreateRoomType(id, "Double");

        var command = NewCommand(
            id,
            NewDto(
                name: "DOUBLE",
                description: null,
                isActive: null));

        _roomTypeRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomTypeModel?>(roomType));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roomType.Name.Should().Be("DOUBLE");

        await _roomTypeRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _roomTypeRepository
            .Received(1)
            .Update(roomType);
    }

    // ---------------------------------------------------------------
    // Atualização parcial
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenNameIsNull_UpdatesOtherFieldsOnly()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var roomType = CreateRoomType(id, "Double");

        var command = NewCommand(
            id,
            new UpdateRoomTypeDto
            {
                Description = "  New description  ",
                IsActive = false
            });

        _roomTypeRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomTypeModel?>(roomType));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        roomType.Name.Should().Be("Double");
        roomType.Description.Should().Be("New description");
        roomType.IsActive.Should().BeFalse();

        await _roomTypeRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _roomTypeRepository
            .Received(1)
            .Update(roomType);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDescriptionContainsOnlyWhitespace_ClearsDescription()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var roomType = CreateRoomType(id, "Double");

        var command = NewCommand(
            id,
            new UpdateRoomTypeDto
            {
                Description = "   "
            });

        _roomTypeRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomTypeModel?>(roomType));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        roomType.Name.Should().Be("Double");
        roomType.Description.Should().BeNull();
        roomType.IsActive.Should().BeTrue();

        _roomTypeRepository
            .Received(1)
            .Update(roomType);
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

        var command = NewCommand(
            Guid.NewGuid().ToString());

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private readonly DateTimeOffset _createdAt =
        DateTimeOffset.UtcNow.AddDays(-1);

    private static UpdateRoomTypeCommand NewCommand(
        string id,
        UpdateRoomTypeDto? dto = null)
    {
        return new UpdateRoomTypeCommand(
            id,
            dto ?? NewDto());
    }

    private static UpdateRoomTypeDto NewDto(
        string? name = "Updated room type",
        string? description = "Updated description",
        bool? isActive = false)
    {
        return new UpdateRoomTypeDto
        {
            Name = name,
            Description = description,
            IsActive = isActive
        };
    }

    private RoomTypeModel CreateRoomType(
        string id,
        string name)
    {
        return new RoomTypeModel
        {
            Id = id,
            Version = 3,
            CreatedAt = _createdAt,
            UpdatedAt = null,
            Name = name,
            Description = "Existing description",
            IsActive = true
        };
    }

    private void VerifyNoRepositoryInteractions()
    {
        _ = _roomTypeRepository.DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _ = _roomTypeRepository.DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _roomTypeRepository.DidNotReceive()
            .Update(Arg.Any<RoomTypeModel>());
    }
}