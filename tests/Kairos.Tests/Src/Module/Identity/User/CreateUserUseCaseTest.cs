using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Providers;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Identity.User;

public sealed class CreateUserUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _userRepository;
    private readonly CreateUserUseCase _sut;

    public CreateUserUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _userRepository = Substitute.For<IUserRepository>();

        _unitOfWork.UserRepository.Returns(_userRepository);

        _sut = new CreateUserUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Request body is required.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .ExistsByEmail(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .Insert(
                Arg.Any<UserModel>(),
                Arg.Any<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task ExecuteAsync_WhenUsernameIsEmpty_ReturnsValidationFailure(
        string? userName)
    {
        // Arrange
        var dto = CreateDto(userName: userName);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("UserName is required.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .Insert(
                Arg.Any<UserModel>(),
                Arg.Any<string>());
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public async Task ExecuteAsync_WhenUsernameIsTooShort_ReturnsValidationFailure(
        string userName)
    {
        // Arrange
        var dto = CreateDto(userName: userName);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "UserName must be between 3 and 256 characters.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsernameExceeds256Characters_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(
            userName: new string('u', 257));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "UserName must be between 3 and 256 characters.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenEmailIsEmpty_ReturnsValidationFailure(
        string? email)
    {
        // Arrange
        var dto = CreateDto(email: email);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Email is required.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .ExistsByEmail(Arg.Any<string>());
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("user@")]
    [InlineData("@example.com")]
    [InlineData("user example.com")]
    public async Task ExecuteAsync_WhenEmailIsInvalid_ReturnsValidationFailure(
        string email)
    {
        // Arrange
        var dto = CreateDto(email: email);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Email must be a valid email address.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailExceeds256Characters_ReturnsValidationFailure()
    {
        // Arrange
        var email = $"{new string('a', 250)}@example.com";
        var dto = CreateDto(email: email);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Email must be a valid email address.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("        ")]
    public async Task ExecuteAsync_WhenPasswordIsEmpty_ReturnsValidationFailure(
        string? password)
    {
        // Arrange
        var dto = CreateDto(password: password);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Password is required.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("short")]
    public async Task ExecuteAsync_WhenPasswordHasFewerThan8Characters_ReturnsValidationFailure(
        string password)
    {
        // Arrange
        var dto = CreateDto(password: password);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Password must contain at least 8 characters.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFullNameExceeds150Characters_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(
            fullName: new string('A', 151));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "FullName cannot exceed 150 characters.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPhoneNumberIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(
            phoneNumber: "not-a-phone");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("PhoneNumber is invalid.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPhoneNumberExceeds30Characters_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(
            phoneNumber: new string('1', 31));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("PhoneNumber is invalid.");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsernameAlreadyExists_ReturnsConflict()
    {
        // Arrange
        const string userName = "existing-user";
        var dto = CreateDto(userName: userName);

        _userRepository
            .ExistsByUsername(userName)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Username is already taken.");

        await _userRepository
            .Received(1)
            .ExistsByUsername(userName);

        await _userRepository
            .DidNotReceive()
            .ExistsByEmail(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .Insert(
                Arg.Any<UserModel>(),
                Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailAlreadyExists_ReturnsConflict()
    {
        // Arrange
        const string email = "existing@example.com";
        var dto = CreateDto(email: email);

        _userRepository
            .ExistsByUsername(dto.UserName)
            .Returns(Task.FromResult(false));

        _userRepository
            .ExistsByEmail(email)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Email is already registered.");

        await _userRepository
            .Received(1)
            .ExistsByUsername(dto.UserName);

        await _userRepository
            .Received(1)
            .ExistsByEmail(email);

        await _userRepository
            .DidNotReceive()
            .Insert(
                Arg.Any<UserModel>(),
                Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsValid_CreatesUser()
    {
        // Arrange
        var dto = CreateDto(
            fullName: "  Anderson Carvalho  ",
            userName: "  anderson  ",
            email: "  anderson@example.com  ",
            password: "Password123!",
            phoneNumber: null);

        SetupNoDuplicates(
            "anderson",
            "anderson@example.com");

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(Task.FromResult(IdentityResult.Success));

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(dto);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert: resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value.Should().NotBeNull();

        var user = result.Value!;

        // Assert: dados normalizados
        user.UserName.Should().Be("anderson");
        user.Email.Should().Be("anderson@example.com");
        user.FullName.Should().Be("Anderson Carvalho");
        user.PhoneNumber.Should().BeNull();

        // Assert: valores iniciais
        user.Id.Should().NotBeNullOrWhiteSpace();
        user.Permissions.Should().BeEmpty();
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();

        user.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        user.CreatedAt.Should().BeOnOrBefore(afterExecution);
        user.UpdatedAt.Should().BeNull();

        // Assert: verificações de unicidade
        await _userRepository
            .Received(1)
            .ExistsByUsername("anderson");

        await _userRepository
            .Received(1)
            .ExistsByEmail("anderson@example.com");

        // Assert: criação e envio da senha original ao Identity
        await _userRepository
            .Received(1)
            .Insert(
                Arg.Is<UserModel>(entity =>
                    entity.Id == user.Id &&
                    entity.UserName == "anderson" &&
                    entity.Email == "anderson@example.com" &&
                    entity.FullName == "Anderson Carvalho" &&
                    entity.Permissions.Length == 0),
                "Password123!");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsernameHasSurroundingWhitespace_UsesTrimmedUsername()
    {
        // Arrange
        var dto = CreateDto(userName: "  new-user  ");

        SetupNoDuplicates(
            "new-user",
            dto.Email);

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.UserName.Should().Be("new-user");

        await _userRepository
            .Received(1)
            .ExistsByUsername("new-user");

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername("  new-user  ");
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailHasSurroundingWhitespace_UsesTrimmedEmail()
    {
        // Arrange
        var dto = CreateDto(email: "  new@example.com  ");

        SetupNoDuplicates(
            dto.UserName,
            "new@example.com");

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("new@example.com");

        await _userRepository
            .Received(1)
            .ExistsByEmail("new@example.com");

        await _userRepository
            .DidNotReceive()
            .ExistsByEmail("  new@example.com  ");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFullNameIsWhitespace_SetsFullNameToNull()
    {
        // Arrange
        var dto = CreateDto(fullName: "   ");

        SetupNoDuplicates(dto.UserName, dto.Email);

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FullName.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPhoneNumberIsWhitespace_SetsPhoneNumberToNull()
    {
        // Arrange
        var dto = CreateDto(phoneNumber: "   ");

        SetupNoDuplicates(dto.UserName, dto.Email);

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentityReturnsDuplicateUserName_ReturnsConflict()
    {
        // Arrange
        var dto = CreateDto();

        SetupNoDuplicates(dto.UserName, dto.Email);

        var identityResult = IdentityResult.Failed(
            new IdentityError
            {
                Code = "DuplicateUserName",
                Description = "Username is already taken."
            });

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(Task.FromResult(identityResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Username is already taken.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentityReturnsDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var dto = CreateDto();

        SetupNoDuplicates(dto.UserName, dto.Email);

        var identityResult = IdentityResult.Failed(
            new IdentityError
            {
                Code = "DuplicateEmail",
                Description = "Email is already registered."
            });

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(Task.FromResult(identityResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Email is already registered.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentityValidationFails_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto();

        SetupNoDuplicates(dto.UserName, dto.Email);

        var identityResult = IdentityResult.Failed(
            new IdentityError
            {
                Code = "PasswordTooShort",
                Description = "Password does not meet the requirements."
            },
            new IdentityError
            {
                Code = "PasswordRequiresDigit",
                Description = "Password must contain a digit."
            });

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(Task.FromResult(identityResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Password does not meet the requirements.");
        result.Errors.Should().Contain(
            "Password must contain a digit.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentityFailsWithoutErrors_ReturnsInternalFailure()
    {
        // Arrange
        var dto = CreateDto();

        SetupNoDuplicates(dto.UserName, dto.Email);

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(
                Task.FromResult(
                    IdentityResult.Failed()));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(500);
        result.Errors.Should().Contain(
            "User could not be created.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsernameLookupThrows_PropagatesException()
    {
        // Arrange
        var dto = CreateDto();

        _userRepository
            .ExistsByUsername(dto.UserName)
            .Returns(
                Task.FromException<bool>(
                    new InvalidOperationException(
                        "Username lookup failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Username lookup failed.");

        await _userRepository
            .DidNotReceive()
            .ExistsByEmail(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .Insert(
                Arg.Any<UserModel>(),
                Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailLookupThrows_PropagatesException()
    {
        // Arrange
        var dto = CreateDto();

        _userRepository
            .ExistsByUsername(dto.UserName)
            .Returns(Task.FromResult(false));

        _userRepository
            .ExistsByEmail(dto.Email)
            .Returns(
                Task.FromException<bool>(
                    new InvalidOperationException(
                        "Email lookup failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Email lookup failed.");

        await _userRepository
            .DidNotReceive()
            .Insert(
                Arg.Any<UserModel>(),
                Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInsertThrows_PropagatesException()
    {
        // Arrange
        var dto = CreateDto();

        SetupNoDuplicates(dto.UserName, dto.Email);

        _userRepository
            .Insert(
                Arg.Any<UserModel>(),
                dto.Password)
            .Returns(
                Task.FromException<IdentityResult>(
                    new InvalidOperationException(
                        "User creation failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("User creation failed.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsAlreadyRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var dto = CreateDto();

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _userRepository
            .DidNotReceive()
            .ExistsByUsername(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .ExistsByEmail(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .Insert(
                Arg.Any<UserModel>(),
                Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledAfterDuplicateChecks_DoesNotInsertUser()
    {
        // Arrange
        var dto = CreateDto();

        using var cts = new CancellationTokenSource();

        _userRepository
            .ExistsByUsername(dto.UserName)
            .Returns(Task.FromResult(false));

        _userRepository
            .ExistsByEmail(dto.Email)
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _userRepository
            .DidNotReceive()
            .Insert(
                Arg.Any<UserModel>(),
                Arg.Any<string>());
    }

    private void SetupNoDuplicates(string userName, string email)
    {
        _userRepository
            .ExistsByUsername(userName)
            .Returns(Task.FromResult(false));

        _userRepository
            .ExistsByEmail(email)
            .Returns(Task.FromResult(false));
    }

    private static CreateUserDTO CreateDto(
        string? userName = "new-user",
        string? email = "new@example.com",
        string? password = "Password123!",
        string? fullName = "Test User",
        string? phoneNumber = null)
    {
        return new CreateUserDTO
        {
            UserName = userName!,
            Email = email!,
            Password = password!,
            FullName = fullName,
            PhoneNumber = phoneNumber
        };
    }
}