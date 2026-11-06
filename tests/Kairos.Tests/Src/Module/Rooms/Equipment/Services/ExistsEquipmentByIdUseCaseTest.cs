using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Equipment.Services.Providers;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Rooms.Equipment.Services;

public sealed class ExistsEquipmentByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly ExistsEquipmentByIdUseCase _sut;

    public ExistsEquipmentByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _equipmentRepository = Substitute.For<IEquipmentRepository>();

        _unitOfWork.EquipmentRepository.Returns(_equipmentRepository);

        _sut = new ExistsEquipmentByIdUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenEquipmentExists_ReturnsTrue()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        _equipmentRepository
            .ExistsAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        await _equipmentRepository
            .Received(1)
            .ExistsAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEquipmentDoesNotExist_ReturnsFalse()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        _equipmentRepository
            .ExistsAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeFalse();
        result.StatusCode.Should().Be(200);

        await _equipmentRepository
            .Received(1)
            .ExistsAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();

        _equipmentRepository
            .ExistsAsync(id, cts.Token)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(id, cts.Token);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();

        await _equipmentRepository
            .Received(1)
            .ExistsAsync(id, cts.Token);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Equipment ID is required.");

        await _equipmentRepository
            .DidNotReceive()
            .ExistsAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("123")]
    [InlineData("not-a-uuid")]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Id must be a valid UUID.");

        await _equipmentRepository
            .DidNotReceive()
            .ExistsAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
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

        string id = Guid.NewGuid().ToString();

        // Act
        var act = () => _sut.ExecuteAsync(id, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _equipmentRepository
            .DidNotReceive()
            .ExistsAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }
}