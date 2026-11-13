using Kairos.Api.Config.Middleware.Exceptions.Abstractions;
using Kairos.Api.Config.Middleware.Exceptions.Handlers;

namespace Kairos.Api.Config.Middleware.Exceptions;

public static class ExceptionHandlerExtensions
{
    public static IServiceCollection AddApiExceptionHandlers(this IServiceCollection services)
    {
        services.AddSingleton<ExceptionResponseWriter>();

        // Entity Framework
        services.AddExceptionHandler<EntityFrameworkExceptionHandler>();

        // Application exceptions
        services.AddExceptionHandler<ApplicationBootstrapperExceptionHandler>();
        services.AddExceptionHandler<InternalServerExceptionHandler>();
        services.AddExceptionHandler<NotFoundExceptionHandler>();

        // Authentication
        services.AddExceptionHandler<UnauthenticatedExceptionHandler>();
        services.AddExceptionHandler<UserNotAuthenticatedExceptionHandler>();

        // Global fallback - always last.
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddProblemDetails();

        return services;
    }
}