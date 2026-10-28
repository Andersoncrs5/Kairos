using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Commands;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.FindPermissionUser.Services;

public sealed class UpdatePermissionUserUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionUserRepository _repository;
    private readonly UpdatePermissionUserUseCase _sut;

    public UpdatePermissionUserUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _repository = Substitute.For<IPermissionUserRepository>();

        _unitOfWork.PermissionUserRepository.Returns(_repository);

        _sut = new UpdatePermissionUserUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        // Arrange
        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        var command = new UpdatePermissionUserCommand(id, dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id is required.");

        await _repository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _repository
            .DidNotReceive()
            .Update(Arg.Any<PermissionUserModel>());
    }

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure(
        string id)
    {
        // Arrange
        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        var command = new UpdatePermissionUserCommand(id, dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id must be a valid UUID.");

        await _repository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _repository
            .DidNotReceive()
            .Update(Arg.Any<PermissionUserModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsValidationFailure()
    {
        // Arrange
        UpdatePermissionUserCommand command = null!;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id is required.");

        await _repository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _repository
            .DidNotReceive()
            .Update(Arg.Any<PermissionUserModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var command = new UpdatePermissionUserCommand(id, null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Request body is required.");

        await _repository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _repository
            .DidNotReceive()
            .Update(Arg.Any<PermissionUserModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssociationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        var command = new UpdatePermissionUserCommand(id, dto);

        _repository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionUserModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(command, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain(
            "Permission-user association not found.");

        await _repository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        _repository
            .DidNotReceive()
            .Update(Arg.Any<PermissionUserModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_UpdatesAssociation()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;
        var association = CreatePermissionUser(id);

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        var command = new UpdatePermissionUserCommand(id, dto);

        _repository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionUserModel?>(association));

        // Act
        var result = await _sut.ExecuteAsync(command, cancellationToken);

        // Assert: resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(association);

        // Assert: campo alterado
        association.IsActive.Should().BeFalse();

        // Assert: campos que não fazem parte do DTO
        association.Id.Should().Be(id);
        association.UserId.Should().Be("user-test-id");
        association.PermissionId.Should().Be("permission-test-id");

        // A auditoria e a versão serão atualizadas pelo AppDbContext.
        association.Version.Should().Be(1);
        association.UpdatedAt.Should().BeNull();

        // Assert: busca feita exatamente uma vez
        await _repository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        // Assert: Update chamado uma vez com a mesma entidade
        _repository
            .Received(1)
            .Update(
                Arg.Is<PermissionUserModel>(
                    entity => ReferenceEquals(entity, association)));

        // O commit fica com o serviço orquestrador.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIsActiveIsNull_PreservesCurrentValue()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var association = CreatePermissionUser(id);

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = null
        };

        var command = new UpdatePermissionUserCommand(id, dto);

        _repository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionUserModel?>(association));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        association.IsActive.Should().BeTrue();

        _repository
            .Received(1)
            .Update(association);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryLookupThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        var command = new UpdatePermissionUserCommand(id, dto);

        var exception = new InvalidOperationException(
            "Permission-user lookup failed.");

        _repository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<PermissionUserModel?>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission-user lookup failed.");

        _repository
            .DidNotReceive()
            .Update(Arg.Any<PermissionUserModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryUpdateThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var association = CreatePermissionUser(id);

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        var command = new UpdatePermissionUserCommand(id, dto);

        var exception = new InvalidOperationException(
            "Permission-user update failed.");

        _repository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionUserModel?>(association));

        _repository
            .When(repository => repository.Update(association))
            .Do(_ => throw exception);

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission-user update failed.");

        _repository
            .Received(1)
            .Update(association);
    }

    private static PermissionUserModel CreatePermissionUser(string id)
    {
        return new PermissionUserModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = null,
            UserId = "user-test-id",
            PermissionId = "permission-test-id",
            IsActive = true
        };
    }
}