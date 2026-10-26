using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public sealed class DeleteUserByIdUseCase(IUnitOfWork uow)
    : IDeleteUserByIdUseCase
{
    public async Task<Result<object>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<object>.ValidationFailure(
                ["Id is required."]);
        }

        var normalizedId = id.Trim();

        var user = await uow.UserRepository.GetByIdAsync(normalizedId);

        if (user is null)
        {
            return Result<object>.NotFound("User not found.");
        }

        var deleteResult = await uow.UserRepository.Delete(user);

        if (!deleteResult.Succeeded)
        {
            var errors = deleteResult.Errors
                .Select(error => error.Description)
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .ToArray();

            var message = errors.Length > 0
                ? string.Join(" ", errors)
                : "User could not be deleted.";

            return Result<object>.Conflict(message);
        }

        return Result<object>.Success(true);
    }
}