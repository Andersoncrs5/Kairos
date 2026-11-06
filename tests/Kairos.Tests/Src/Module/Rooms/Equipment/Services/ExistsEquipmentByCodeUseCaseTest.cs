using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Equipment.Services.Providers;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Rooms.Equipment.Services;

public sealed class ExistsEquipmentByCodeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly ExistsEquipmentByCodeUseCase _sut;

    public ExistsEquipmentByCodeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _equipmentRepository = Substitute.For<IEquipmentRepository>();

        _unitOfWork.EquipmentRepository.Returns(_equipmentRepository);

        _sut = new ExistsEquipmentByCodeUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenEquipmentExistsByCode_ReturnsTrue()
    {
        // Arrange
        string code = "EQ-001";

        _equipmentRepository
            .ExistsByCodeAsync(code, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(code);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        await _equipmentRepository
            .Received(1)
            .ExistsByCodeAsync(code, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEquipmentDoesNotExistByCode_ReturnsFalse()
    {
        // Arrange
        string code = "EQ-001";

        _equipmentRepository
            .ExistsByCodeAsync(code, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(code);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeFalse();
        result.StatusCode.Should().Be(200);

        await _equipmentRepository
            .Received(1)
            .ExistsByCodeAsync(code, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        // Arrange
        string code = "EQ-001";
        using var cts = new CancellationTokenSource();

        _equipmentRepository
            .ExistsByCodeAsync(code, cts.Token)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(code, cts.Token);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();

        await _equipmentRepository
            .Received(1)
            .ExistsByCodeAsync(code, cts.Token);
    }

    // ---------------------------------------------------------------
    // Validação do código
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenCodeIsEmpty_ReturnsValidationFailure(
        string code)
    {
        // Act
        var result = await _sut.ExecuteAsync(code);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Equipment code is required.");

        await _equipmentRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
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

        string code = "EQ-001";

        // Act
        var act = () => _sut.ExecuteAsync(code, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _equipmentRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }
}