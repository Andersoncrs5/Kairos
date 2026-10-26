using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;
using Kairos.Api.Modules.Identity.Permissions.Services.Provider;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Interface;
using Kairos.Api.Modules.Identity.Users.Services.Providers;
using Microsoft.AspNetCore.Identity;

namespace Kairos.Api.Modules.Identity;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services)
    {
        services.AddDataProtection();

        // ASP.NET Core Identity
        services
            .AddIdentityCore<UserModel>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IPermissionUserRepository, PermissionUserRepository>();

        // Auth Use Cases
        services.AddScoped<ICreateRefreshTokenUseCase, CreateRefreshTokenUseCase>();
        services.AddScoped<ICreateTokenUseCase, CreateTokenUseCase>();
        services.AddScoped<IGetPrincipalFromExpiredTokenUseCase, GetPrincipalFromExpiredTokenUseCase>();
        services.AddScoped<ILoginUserUseCase, LoginUserUseCase>();
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<ILogoutUseCase, LogoutUseCase>();
        services.AddScoped<IRefreshTokenUseCase, RefreshTokenUseCase>();
        services.AddScoped<IChangePasswordUserUseCase, ChangePasswordUserUseCase>();

        // User Use Cases
        services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();
        services.AddScoped<IDeleteUserByIdUseCase, DeleteUserByIdUseCase>();
        services.AddScoped<IExistsUserByIdUseCase, ExistsUserByIdUseCase>();
        services.AddScoped<IFindUserByIdUseCase, FindUserByIdUseCase>();
        services.AddScoped<IFindUsersUseCase, FindUsersUseCase>();
        services.AddScoped<IUpdateUserUseCase, UpdateUserUseCase>();

        // Permission Use Cases
        services.AddScoped<ICreatePermissionUseCase, CreatePermissionUseCase>();
        services.AddScoped<IDeletePermissionByIdUseCase, DeletePermissionByIdUseCase>();
        services.AddScoped<IExistsPermissionByCodeUseCase, ExistsPermissionByCodeUseCase>();
        services.AddScoped<IFindPermissionByCodeUseCase, FindPermissionByCodeUseCase>();
        services.AddScoped<IFindPermissionByIdUseCase, FindPermissionByIdUseCase>();
        services.AddScoped<IUpdatePermissionUseCase, UpdatePermissionUseCase>();

        // PermissionUser Use Cases
        services.AddScoped<ICreatePermissionUserUseCase, CreatePermissionUserUseCase>();
        services.AddScoped<IDeletePermissionUserByUserIdAndPermissionIdUseCase, DeletePermissionUserByUserIdAndPermissionIdUseCase>();
        services.AddScoped<IExistsPermissionUserByUserIdAndPermissionIdUseCase, ExistsPermissionUserByUserIdAndPermissionIdUseCase>();
        services.AddScoped<IFindPermissionUserByIdUseCase, FindPermissionUserByIdUseCase>();
        services.AddScoped<IUpdatePermissionUserUseCase, UpdatePermissionUserUseCase>();

        return services;
    }
}