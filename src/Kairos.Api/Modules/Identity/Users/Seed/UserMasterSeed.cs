using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Seed;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Modules.Identity.Users.Seed;

public sealed class UserMasterSeed(
    IUnitOfWork uow,
    IOptions<UserMasterOptions> options,
    ILogger<UserMasterSeed> logger)
    : IUserMasterSeed
{
    public async Task Execute(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UserMasterOptions configuration = options.Value;
        string userName = configuration.UserName.Trim();
        string email = configuration.Email.Trim();

        string[] permissions = PermissionSeeder.Permissions
            .Select(permission => permission.Code)
            .ToArray();

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        string action = "unchanged";

        logger.LogInformation(
            "Starting master user seeding. " +
            "UserName: {UserName}; Email: {Email}; " +
            "PermissionCount: {PermissionCount}",
            userName,
            email,
            permissions.Length);

        await uow.ExecuteTransactionAsync(
            async ct =>
            {
                ct.ThrowIfCancellationRequested();

                UserModel? userByUserName =
                    await uow.UserRepository.GetByUsername(userName);

                UserModel? userByEmail =
                    await uow.UserRepository.GetByEmail(email);

                // 1. Create the master user if neither username nor email exists.
                if (userByUserName is null && userByEmail is null)
                {
                    UserModel user = new()
                    {
                        UserName = userName,
                        Email = email,
                        FullName = configuration.FullName.Trim(),
                        Permissions = permissions,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    IdentityResult createResult =
                        await uow.UserRepository.Insert(
                            user,
                            configuration.Password);

                    EnsureSucceeded(
                        createResult,
                        "Could not create the master user.");

                    action = "created";

                    return;
                }

                // 2. Ensure the configured username and email belong to the same user.
                if (userByUserName is null ||
                    userByEmail is null ||
                    userByUserName.Id != userByEmail.Id)
                {
                    throw new InvalidOperationException(
                        "The configured master username or email " +
                        "belongs to a different user.");
                }

                UserModel masterUser = userByUserName;

                // 3. Check whether the permission array needs updating.
                bool permissionsChanged =
                    masterUser.Permissions is null ||
                    !masterUser.Permissions
                        .OrderBy(
                            permission => permission,
                            StringComparer.OrdinalIgnoreCase)
                        .SequenceEqual(
                            permissions.OrderBy(
                                permission => permission,
                                StringComparer.OrdinalIgnoreCase),
                            StringComparer.OrdinalIgnoreCase);

                if (!permissionsChanged)
                {
                    return;
                }

                // 4. Update the master user's permission array.
                masterUser.Permissions = permissions;

                IdentityResult updateResult =
                    await uow.UserRepository.Update(masterUser);

                EnsureSucceeded(
                    updateResult,
                    "Could not update the master user's permissions.");

                action = "updated";
            },
            cancellationToken);

        // Log completion only after the transaction has succeeded.
        TimeSpan elapsed = DateTimeOffset.UtcNow - startedAt;

        logger.LogInformation(
            "Master user seeding completed. " +
            "UserName: {UserName}; Email: {Email}; " +
            "Action: {Action}; PermissionCount: {PermissionCount}; " +
            "DurationMs: {DurationMs}",
            userName,
            email,
            action,
            permissions.Length,
            elapsed.TotalMilliseconds);
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join(
            "; ",
            result.Errors.Select(
                error => $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException($"{message} {errors}");
    }
}