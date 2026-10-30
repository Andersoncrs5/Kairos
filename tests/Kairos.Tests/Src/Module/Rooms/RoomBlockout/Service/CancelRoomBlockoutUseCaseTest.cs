using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;
using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomBlockout.Service;

public sealed class CancelRoomBlockoutUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoomBlockoutRepository _roomBlockoutRepository;
    private readonly CancelRoomBlockoutUseCase _sut;

    public CancelRoomBlockoutUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _roomBlockoutRepository = Substitute.For<IRoomBlockoutRepository>();

        _unitOfWork.RoomBlockoutRepository.Returns(_roomBlockoutRepository);

        _sut = new CancelRoomBlockoutUseCase(_unitOfWork);
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
            "Room blockout cancellation command is required.");

        VerifyNoRepositoryInteractions();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenRoomBlockoutIdIsEmpty_ReturnsValidationFailure(
        string roomBlockoutId)
    {
        // Arrange
        var command = NewCommand(roomBlockoutId: roomBlockoutId);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout ID is required.");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomBlockoutIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            roomBlockoutId: "invalid-uuid");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout ID must be a valid UUID.");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationReasonIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            cancellationReason: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout cancellation reason is required.");

        VerifyNoRepositoryInteractions();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenCancellationReasonIsEmpty_ReturnsValidationFailure(
        string cancellationReason)
    {
        // Arrange
        var command = NewCommand(
            cancellationReason: cancellationReason);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout cancellation reason is required.");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationReasonExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            cancellationReason: new string('a', 256));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout cancellation reason cannot exceed 255 characters.");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledByIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new CancelRoomBlockoutCommand(
            Guid.NewGuid().ToString(),
            "Scheduled cancellation",
            null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout canceller ID is required.");

        VerifyNoRepositoryInteractions();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenCancelledByIsEmpty_ReturnsValidationFailure(
        string cancelledBy)
    {
        // Arrange
        var command = NewCommand(
            cancelledBy: cancelledBy);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room blockout canceller ID is required.");

        VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledByIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            cancelledBy: "invalid-uuid");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Cancelled by must be a valid UUID.");

        VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Bloqueio inexistente ou já cancelado
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomBlockoutDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var roomBlockoutId = Guid.NewGuid().ToString();

        var command = NewCommand(
            roomBlockoutId: roomBlockoutId);

        _roomBlockoutRepository
            .GetByIdAsync(
                roomBlockoutId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomBlockoutModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain(
            "Room blockout not found.");

        await _roomBlockoutRepository
            .Received(1)
            .GetByIdAsync(
                roomBlockoutId,
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomBlockoutIsAlreadyCancelled_ReturnsConflict()
    {
        // Arrange
        var roomBlockoutId = Guid.NewGuid().ToString();

        var blockout = CreateRoomBlockout(
            roomBlockoutId,
            status: RoomBlockoutStatus.Cancelled);

        var previousCancelledAt = DateTimeOffset.UtcNow.AddDays(-2);

        blockout.CancelledAt = previousCancelledAt;
        blockout.CancellationReason = "Previously cancelled";
        blockout.CancelledBy = Guid.NewGuid().ToString();

        var previousReason = blockout.CancellationReason;
        var previousCancelledBy = blockout.CancelledBy;

        var command = NewCommand(
            roomBlockoutId: roomBlockoutId);

        _roomBlockoutRepository
            .GetByIdAsync(
                roomBlockoutId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomBlockoutModel?>(blockout));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Room blockout is already cancelled.");

        blockout.Status.Should().Be(RoomBlockoutStatus.Cancelled);
        blockout.CancelledAt.Should().Be(previousCancelledAt);
        blockout.CancellationReason.Should().Be(previousReason);
        blockout.CancelledBy.Should().Be(previousCancelledBy);

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }

    // ---------------------------------------------------------------
    // Cancelamento bem-sucedido
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_CancelsRoomBlockout()
    {
        // Arrange
        var roomBlockoutId = Guid.NewGuid();
        var cancelledBy = Guid.NewGuid();

        var blockout = CreateRoomBlockout(
            roomBlockoutId.ToString(),
            status: RoomBlockoutStatus.Scheduled);

        var originalRoomId = blockout.RoomId;
        var originalType = blockout.Type;
        var originalReason = blockout.Reason;
        var originalNotes = blockout.Notes;
        var originalStartsAt = blockout.StartsAt;
        var originalEndsAt = blockout.EndsAt;
        var originalCreatedBy = blockout.CreatedBy;
        var originalCreatedAt = blockout.CreatedAt;
        var originalVersion = blockout.Version;

        var command = NewCommand(
            roomBlockoutId: roomBlockoutId.ToString("B"),
            cancellationReason: "  Maintenance was postponed.  ",
            cancelledBy: cancelledBy.ToString("B"));

        using var cts = new CancellationTokenSource();

        var ct = cts.Token;

        _roomBlockoutRepository
            .GetByIdAsync(
                roomBlockoutId.ToString(),
                ct)
            .Returns(Task.FromResult<RoomBlockoutModel?>(blockout));

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(blockout);

        blockout.Status.Should().Be(RoomBlockoutStatus.Cancelled);

        blockout.CancelledAt.Should().NotBeNull();
        blockout.CancelledAt.Should().BeOnOrAfter(beforeExecution);
        blockout.CancelledAt.Should().BeOnOrBefore(afterExecution);

        blockout.CancellationReason.Should().Be(
            "Maintenance was postponed.");

        blockout.CancelledBy.Should().Be(cancelledBy.ToString());

        // Os dados originais do bloqueio devem ser preservados.
        blockout.Id.Should().Be(roomBlockoutId.ToString());
        blockout.RoomId.Should().Be(originalRoomId);
        blockout.Type.Should().Be(originalType);
        blockout.Reason.Should().Be(originalReason);
        blockout.Notes.Should().Be(originalNotes);
        blockout.StartsAt.Should().Be(originalStartsAt);
        blockout.EndsAt.Should().Be(originalEndsAt);
        blockout.CreatedBy.Should().Be(originalCreatedBy);
        blockout.CreatedAt.Should().Be(originalCreatedAt);
        blockout.Version.Should().Be(originalVersion);

        await _roomBlockoutRepository
            .Received(1)
            .GetByIdAsync(
                roomBlockoutId.ToString(),
                ct);

        _roomBlockoutRepository
            .Received(1)
            .Update(blockout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationReasonContainsWhitespace_TrimsReason()
    {
        // Arrange
        var roomBlockoutId = Guid.NewGuid().ToString();

        var blockout = CreateRoomBlockout(roomBlockoutId);

        var command = NewCommand(
            roomBlockoutId: roomBlockoutId,
            cancellationReason: "   Cancelled by administrator   ");

        _roomBlockoutRepository
            .GetByIdAsync(
                roomBlockoutId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomBlockoutModel?>(blockout));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        blockout.CancellationReason.Should().Be(
            "Cancelled by administrator");

        _roomBlockoutRepository
            .Received(1)
            .Update(blockout);
    }

    // ---------------------------------------------------------------
    // Cancelamento da execução
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        cts.Cancel();

        var command = NewCommand();

        // Act
        var act = () => _sut.ExecuteAsync(
            command,
            cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static CancelRoomBlockoutCommand NewCommand(
        string? roomBlockoutId = null,
        string? cancellationReason = "Scheduled cancellation",
        string? cancelledBy = null)
    {
        return new CancelRoomBlockoutCommand(
            roomBlockoutId ?? Guid.NewGuid().ToString(),
            cancellationReason!,
            cancelledBy ?? Guid.NewGuid().ToString());
    }

    private static RoomBlockoutModel CreateRoomBlockout(
        string id,
        RoomBlockoutStatus status = RoomBlockoutStatus.Scheduled)
    {
        var now = DateTimeOffset.UtcNow;

        return new RoomBlockoutModel
        {
            Id = id,
            Version = 3,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = null,

            RoomId = Guid.NewGuid().ToString(),

            Type = RoomBlockoutType.Maintenance,
            Status = status,

            Reason = "Preventive maintenance",
            Notes = "Air conditioning inspection",

            StartsAt = now.AddDays(1),
            EndsAt = now.AddDays(1).AddHours(2),

            CancelledAt = null,
            CancellationReason = null,
            CancelledBy = null,

            CreatedBy = Guid.NewGuid().ToString()
        };
    }

    private void VerifyNoRepositoryInteractions()
    {
        _ = _roomBlockoutRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _roomBlockoutRepository
            .DidNotReceive()
            .Update(Arg.Any<RoomBlockoutModel>());
    }
}