using System.Threading.RateLimiting;
using Kairos.Api.Config.RateLimiting.Contracts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.RateLimiting;

public sealed class RateLimitPolicyConfiguration(
    IOptions<RateLimitingOptions> settings)
    : IConfigureOptions<RateLimiterOptions>
{
    public void Configure(RateLimiterOptions options)
    {
        RateLimitingOptions cfg = settings.Value;

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = RateLimitRejectionHandler.HandleAsync;

        // Globais: a requisição precisa passar nos dois
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            CreateGlobalLimiter(cfg),
            CreateGlobalWriteLimiter(cfg));

        // -------- Anônimos: por IP --------
        options.AddPolicy(RateLimitPolicies.AuthLogin,
            ctx => Sliding(RateLimitPartitionKeys.Ip(ctx), cfg.AuthLogin));

        options.AddPolicy(RateLimitPolicies.AuthRegister,
            ctx => Fixed(RateLimitPartitionKeys.Ip(ctx), cfg.AuthRegister));

        options.AddPolicy(RateLimitPolicies.AuthRefresh,
            ctx => Fixed(RateLimitPartitionKeys.Ip(ctx), cfg.AuthRefresh));

        options.AddPolicy(RateLimitPolicies.AuthLookup,
            ctx => Fixed(RateLimitPartitionKeys.Ip(ctx), cfg.AuthLookup));

        // -------- Autenticados: por usuário --------
        options.AddPolicy(RateLimitPolicies.ChangePassword,
            ctx => Fixed(RateLimitPartitionKeys.UserOrIp(ctx), cfg.ChangePassword));

        options.AddPolicy(RateLimitPolicies.AccountDelete,
            ctx => Fixed(RateLimitPartitionKeys.UserOrIp(ctx), cfg.AccountDelete));

        options.AddPolicy(RateLimitPolicies.ReservationCreate,
            ctx => Fixed(RateLimitPartitionKeys.UserOrIp(ctx), cfg.ReservationCreate));

        options.AddPolicy(RateLimitPolicies.Availability,
            ctx => Bucket(RateLimitPartitionKeys.UserOrIp(ctx), cfg.Availability));

        options.AddPolicy(RateLimitPolicies.CheckIn,
            ctx => Fixed(RateLimitPartitionKeys.UserOrIp(ctx), cfg.CheckIn));

        options.AddPolicy(RateLimitPolicies.FeedbackCreate,
            ctx => Fixed(RateLimitPartitionKeys.UserOrIp(ctx), cfg.FeedbackCreate));

        options.AddPolicy(RateLimitPolicies.Admin,
            ctx => Fixed(RateLimitPartitionKeys.UserOrIp(ctx), cfg.Admin));

        options.AddPolicy(RateLimitPolicies.HeavyRead,
            ctx => Bucket(RateLimitPartitionKeys.UserOrIp(ctx), cfg.HeavyRead));
    }

    // ----------------------------------------------------------
    // Globais
    // ----------------------------------------------------------

    private static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter(
        RateLimitingOptions cfg)
        => PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            IsExempt(ctx, cfg)
                ? RateLimitPartition.GetNoLimiter("exempt")
                : Fixed(RateLimitPartitionKeys.UserOrIp(ctx), cfg.Global));

    private static PartitionedRateLimiter<HttpContext> CreateGlobalWriteLimiter(
        RateLimitingOptions cfg)
        => PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            IsExempt(ctx, cfg) || !IsMutation(ctx.Request.Method)
                ? RateLimitPartition.GetNoLimiter("not-a-write")
                : Fixed(RateLimitPartitionKeys.UserOrIp(ctx), cfg.GlobalWrite));

    private static bool IsExempt(HttpContext ctx, RateLimitingOptions cfg)
    {
        // Preflight de CORS não pode gastar cota
        if (HttpMethods.IsOptions(ctx.Request.Method))
        {
            return true;
        }

        return cfg.ExemptPaths.Any(path =>
            ctx.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMutation(string method)
        => !HttpMethods.IsGet(method)
           && !HttpMethods.IsHead(method)
           && !HttpMethods.IsOptions(method);

    // ----------------------------------------------------------
    // Algoritmos
    // ----------------------------------------------------------

    private static RateLimitPartition<string> Fixed(string key, RateLimitRule rule)
        => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });

    // Evita a "rajada dupla" na virada da janela fixa; bom para login
    private static RateLimitPartition<string> Sliding(string key, RateLimitRule rule)
        => RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            SegmentsPerWindow = rule.SegmentsPerWindow,
            QueueLimit = 0,
            AutoReplenishment = true
        });

    // Aceita rajadas até a capacidade e recarrega de forma contínua;
    // bom para telas que consultam várias vezes seguidas
    private static RateLimitPartition<string> Bucket(string key, RateLimitRule rule)
        => RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = rule.PermitLimit,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(
                (double)rule.WindowSeconds / rule.PermitLimit),
            QueueLimit = 0,
            AutoReplenishment = true
        });
}