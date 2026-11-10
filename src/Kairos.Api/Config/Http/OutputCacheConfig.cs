using Microsoft.AspNetCore.OutputCaching;

namespace Kairos.Api.Config.Http;

public static class OutputCacheConfig
{
    public const string Categories = "categories";
    public const string RoomTypes = "room-types";

    public static IServiceCollection AddOutputCacheConfig(
        this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            options.AddPolicy(
                Categories,
                b => b.AddPolicy<AuthenticatedReadPolicy>()
                      .Expire(TimeSpan.FromMinutes(5))
                      .Tag(Categories),
                excludeDefaultPolicy: true);

            options.AddPolicy(
                RoomTypes,
                b => b.AddPolicy<AuthenticatedReadPolicy>()
                      .Expire(TimeSpan.FromMinutes(5))
                      .Tag(RoomTypes),
                excludeDefaultPolicy: true);
        });

        return services;
    }
}

public sealed class AuthenticatedReadPolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(
        OutputCacheContext context, CancellationToken ct)
    {
        HttpRequest request = context.HttpContext.Request;

        bool cacheable = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);

        context.EnableOutputCaching = cacheable;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;

        context.CacheVaryByRules.QueryKeys = "*";

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(
        OutputCacheContext context, CancellationToken ct)
        => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(
        OutputCacheContext context, CancellationToken ct)
    {
        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK)
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}