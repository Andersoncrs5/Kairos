using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.Room.Services.Providers;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Moq;

namespace Kairos.Tests.Module.Rooms.Room.Services;

public class UpdateRoomUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IRoomRepository> _roomRepository = new();
    private readonly Mock<IRoomTypeRepository> _roomTypeRepository = new();
    private readonly UpdateRoomUseCase _sut;

    public UpdateRoomUseCaseTest()
    {
        _unitOfWork.Setup(uow => uow.RoomRepository)
            .Returns(_roomRepository.Object);

        _unitOfWork.Setup(uow => uow.RoomTypeRepository)
            .Returns(_roomTypeRepository.Object);

        _sut = new UpdateRoomUseCase(_unitOfWork.Object);
    }

    // ------------------------------------------------------------------
    // Validação do command
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsBadRequest()
    {
        var result = await _sut.ExecuteAsync(null!, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room update command is required.");

        VerifyRoomWasNotLoaded();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenRoomIdIsBlank_ReturnsBadRequest(string roomId)
    {
        var command = new UpdateRoomCommand(roomId, new UpdateRoomDto { Name = "Nova sala" });

        var result = await _sut.ExecuteAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room ID is required.");

        VerifyRoomWasNotLoaded();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsBadRequest()
    {
        var command = new UpdateRoomCommand(Guid.NewGuid().ToString(), null!);

        var result = await _sut.ExecuteAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room update data is required.");

        VerifyRoomWasNotLoaded();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsNotAValidUuid_ReturnsBadRequest()
    {
        var command = new UpdateRoomCommand("invalid-uuid", new UpdateRoomDto { Name = "Nova sala" });

        var result = await _sut.ExecuteAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id must be a valid UUID.");

        VerifyRoomWasNotLoaded();
    }

    // ------------------------------------------------------------------
    // Validação do DTO (tamanhos e faixas)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("RoomTypeIdTooLong", "Room type ID cannot exceed 36 characters.")]
    [InlineData("CodeTooLong", "Room code cannot exceed 30 characters.")]
    [InlineData("NameTooLong", "Room name cannot exceed 120 characters.")]
    [InlineData("DescriptionTooLong", "Room description cannot exceed 500 characters.")]
    [InlineData("LocationTooLong", "Room location cannot exceed 150 characters.")]
    [InlineData("FloorTooLong", "Room floor cannot exceed 30 characters.")]
    [InlineData("CapacityZero", "Room capacity must be greater than zero.")]
    [InlineData("AreaZero", "Room area must be greater than zero and cannot exceed 99999999.99.")]
    [InlineData("AreaTooBig", "Room area must be greater than zero and cannot exceed 99999999.99.")]
    public async Task ExecuteAsync_WhenDtoIsInvalid_ReturnsBadRequest(
        string scenario,
        string expectedError)
    {
        var dto = scenario switch
        {
            "RoomTypeIdTooLong" => new UpdateRoomDto { RoomTypeId = new string('a', 37) },
            "CodeTooLong" => new UpdateRoomDto { Code = new string('a', 31) },
            "NameTooLong" => new UpdateRoomDto { Name = new string('a', 121) },
            "DescriptionTooLong" => new UpdateRoomDto { Description = new string('a', 501) },
            "LocationTooLong" => new UpdateRoomDto { Location = new string('a', 151) },
            "FloorTooLong" => new UpdateRoomDto { Floor = new string('a', 31) },
            "CapacityZero" => new UpdateRoomDto { Capacity = 0 },
            "AreaZero" => new UpdateRoomDto { AreaSquareMeters = 0m },
            // Valor bem acima do limite para não depender da cultura da máquina
            "AreaTooBig" => new UpdateRoomDto { AreaSquareMeters = 100_000_000_000m },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(Guid.NewGuid().ToString(), dto),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(expectedError);

        VerifyRoomWasNotLoaded();
    }

    // ------------------------------------------------------------------
    // Campos obrigatórios enviados em branco
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("RoomTypeId", "Room type ID cannot be empty.")]
    [InlineData("Code", "Room code cannot be empty.")]
    [InlineData("Name", "Room name cannot be empty.")]
    [InlineData("Location", "Room location cannot be empty.")]
    public async Task ExecuteAsync_WhenRequiredFieldIsBlank_ReturnsBadRequest(
        string field,
        string expectedError)
    {
        var dto = field switch
        {
            "RoomTypeId" => new UpdateRoomDto { RoomTypeId = "   " },
            "Code" => new UpdateRoomDto { Code = "   " },
            "Name" => new UpdateRoomDto { Name = "   " },
            "Location" => new UpdateRoomDto { Location = "   " },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(Guid.NewGuid().ToString(), dto),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(expectedError);

        VerifyRoomWasNotLoaded();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSeveralRequiredFieldsAreBlank_ReturnsAllErrors()
    {
        var dto = new UpdateRoomDto
        {
            Code = "",
            Name = " ",
            Location = "   "
        };

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(Guid.NewGuid().ToString(), dto),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Room code cannot be empty.");
        result.Errors.Should().Contain("Room name cannot be empty.");
        result.Errors.Should().Contain("Room location cannot be empty.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomTypeIdIsNotAValidUuid_ReturnsBadRequest()
    {
        var dto = new UpdateRoomDto { RoomTypeId = "not-a-guid" };

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(Guid.NewGuid().ToString(), dto),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room type ID must be a valid UUID.");

        VerifyRoomWasNotLoaded();
    }

    // ------------------------------------------------------------------
    // Existência
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        var roomId = Guid.NewGuid().ToString();

        _roomRepository
            .Setup(repo => repo.GetByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomModel?)null);

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(roomId, new UpdateRoomDto { Name = "Nova sala" }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room not found.");

        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewRoomTypeDoesNotExist_ReturnsNotFound()
    {
        var room = ArrangeRoom();
        var newRoomTypeId = Guid.NewGuid().ToString();

        _roomTypeRepository
            .Setup(repo => repo.GetByIdAsync(newRoomTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomTypeModel?)null);

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, new UpdateRoomDto { RoomTypeId = newRoomTypeId }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room type not found.");

        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomTypeIsUnchanged_DoesNotQueryRoomTypeRepository()
    {
        var room = ArrangeRoom();

        // Mesmo tipo, só que em maiúsculas
        var dto = new UpdateRoomDto { RoomTypeId = room.RoomTypeId.ToUpperInvariant() };

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, dto),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _roomTypeRepository.Verify(
            repo => repo.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // Unicidade do código
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCodeAlreadyExists_ReturnsConflict()
    {
        var room = ArrangeRoom();

        _roomRepository
            .Setup(repo => repo.ExistsByCodeAsync("B-202", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Com espaços: a consulta deve usar o código já aparado
        var dto = new UpdateRoomDto { Code = "  B-202  " };

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, dto),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain("Room with the code 'B-202' already exists.");

        // A sala não foi alterada
        room.Code.Should().Be("A-101");

        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeOnlyChangesCasing_DoesNotCheckUniqueness()
    {
        var room = ArrangeRoom();

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, new UpdateRoomDto { Code = "a-101" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        room.Code.Should().Be("a-101");

        _roomRepository.Verify(
            repo => repo.ExistsByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAllFieldsAreProvided_UpdatesRoom()
    {
        var room = ArrangeRoom();
        var newRoomTypeId = Guid.NewGuid();

        _roomTypeRepository
            .Setup(repo => repo.GetByIdAsync(newRoomTypeId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRoomType(newRoomTypeId.ToString()));

        var dto = new UpdateRoomDto
        {
            // Maiúsculas: o use case normaliza para o formato padrão do Guid
            RoomTypeId = newRoomTypeId.ToString().ToUpperInvariant(),
            Code = "  B-202  ",
            Name = "  Sala Beta  ",
            Description = "  Nova descrição  ",
            Location = "  Bloco B  ",
            Floor = "  2º andar  ",
            Capacity = 20,
            AreaSquareMeters = 40.25m,
            IsAccessible = false,
            IsReservable = false,
            IsActive = false
        };

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, dto),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(room);

        room.RoomTypeId.Should().Be(newRoomTypeId.ToString());
        room.Code.Should().Be("B-202");
        room.Name.Should().Be("Sala Beta");
        room.Description.Should().Be("Nova descrição");
        room.Location.Should().Be("Bloco B");
        room.Floor.Should().Be("2º andar");
        room.Capacity.Should().Be(20);
        room.AreaSquareMeters.Should().Be(40.25m);
        room.IsAccessible.Should().BeFalse();
        room.IsReservable.Should().BeFalse();
        room.IsActive.Should().BeFalse();

        _roomRepository.Verify(repo => repo.Update(room), Times.Once);

        // Quem faz o commit é a transação que envolve o use case
        _unitOfWork.Verify(
            uow => uow.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyNameIsProvided_KeepsOtherFieldsUnchanged()
    {
        var room = ArrangeRoom();
        var original = NewRoom(room.Id, room.RoomTypeId);

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, new UpdateRoomDto { Name = "  Sala Gama  " }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        room.Name.Should().Be("Sala Gama");

        room.RoomTypeId.Should().Be(original.RoomTypeId);
        room.Code.Should().Be(original.Code);
        room.Description.Should().Be(original.Description);
        room.Location.Should().Be(original.Location);
        room.Floor.Should().Be(original.Floor);
        room.Capacity.Should().Be(original.Capacity);
        room.AreaSquareMeters.Should().Be(original.AreaSquareMeters);
        room.IsAccessible.Should().Be(original.IsAccessible);
        room.IsReservable.Should().Be(original.IsReservable);
        room.IsActive.Should().Be(original.IsActive);

        // Nada além do nome mudou, então não há o que validar em outros repositórios
        _roomRepository.Verify(
            repo => repo.ExistsByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _roomTypeRepository.Verify(
            repo => repo.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _roomRepository.Verify(repo => repo.Update(room), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOptionalTextIsBlank_ClearsTheField()
    {
        var room = ArrangeRoom();

        var dto = new UpdateRoomDto
        {
            Description = "   ",
            Floor = ""
        };

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, dto),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        room.Description.Should().BeNull();
        room.Floor.Should().BeNull();

        // Os obrigatórios continuam como estavam
        room.Name.Should().Be("Sala Alfa");
        room.Location.Should().Be("Bloco A");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoHasNoFields_SucceedsWithoutChangingTheRoom()
    {
        var room = ArrangeRoom();
        var original = NewRoom(room.Id, room.RoomTypeId);

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, new UpdateRoomDto()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(room);

        room.Should().BeEquivalentTo(original, options => options
            .Excluding(r => r.CreatedAt));

        _roomRepository.Verify(repo => repo.Update(room), Times.Once);
    }

    // ------------------------------------------------------------------
    // Cancelamento
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsBeforeTouchingRepositories()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new UpdateRoomCommand(
            Guid.NewGuid().ToString(),
            new UpdateRoomDto { Name = "Nova sala" });

        Func<Task> act = () => _sut.ExecuteAsync(command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        VerifyRoomWasNotLoaded();
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepositories()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var room = NewRoom();

        _roomRepository
            .Setup(repo => repo.GetByIdAsync(room.Id, token))
            .ReturnsAsync(room);

        var result = await _sut.ExecuteAsync(
            new UpdateRoomCommand(room.Id, new UpdateRoomDto { Name = "Nova sala" }),
            token);

        result.IsSuccess.Should().BeTrue();

        _roomRepository.Verify(repo => repo.GetByIdAsync(room.Id, token), Times.Once);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static RoomModel NewRoom(string? id = null, string? roomTypeId = null)
    {
        return new RoomModel
        {
            Id = id ?? Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            RoomTypeId = roomTypeId ?? Guid.NewGuid().ToString(),
            Code = "A-101",
            Name = "Sala Alfa",
            Description = "Sala de reuniões",
            Location = "Bloco A",
            Floor = "1º andar",
            Capacity = 10,
            AreaSquareMeters = 25.5m,
            IsAccessible = true,
            IsReservable = true,
            IsActive = true
        };
    }

    private static RoomTypeModel NewRoomType(string id)
    {
        return new RoomTypeModel
        {
            Id = id,
            Name = $"Tipo {id}",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Cria uma sala e faz o repositório devolvê-la ao buscar pelo id.</summary>
    private RoomModel ArrangeRoom()
    {
        var room = NewRoom();

        _roomRepository
            .Setup(repo => repo.GetByIdAsync(room.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        return room;
    }

    private void VerifyRoomWasNotLoaded()
    {
        _roomRepository.Verify(
            repo => repo.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyNothingWasUpdated()
    {
        _roomRepository.Verify(
            repo => repo.Update(It.IsAny<RoomModel>()),
            Times.Never);
    }
}