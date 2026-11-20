using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

public sealed class RedisPasskeyFlowStore(
    IConnectionMultiplexer redis,
    IOptions<PasskeyOptions> options)
    : IPasskeyFlowStore
{
    private const string KeyPrefix = "kairos:passkey:flow:";

    public async Task<string> SaveAsync(
        PasskeyFlowKind kind,
        string optionsJson,
        string? userId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        string flowId = Base64Url.EncodeToString(
            RandomNumberGenerator.GetBytes(32));

        string json = JsonSerializer.Serialize(
            new PasskeyFlow(kind, optionsJson, userId));

        await redis.GetDatabase().StringSetAsync(
            KeyPrefix + flowId,
            json,
            TimeSpan.FromSeconds(options.Value.FlowLifetimeSeconds));

        return flowId;
    }

    public async Task<PasskeyFlow?> TakeAsync(
        string flowId,
        PasskeyFlowKind kind,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(flowId) || flowId.Length > 128)
            return null;

        // GETDEL (Redis 6.2+): atomic read-and-delete.
        RedisValue value = await redis.GetDatabase()
            .StringGetDeleteAsync(KeyPrefix + flowId);

        if (value.IsNullOrEmpty)
            return null;

        PasskeyFlow? flow =
            JsonSerializer.Deserialize<PasskeyFlow>(value.ToString());

        return flow is not null && flow.Kind == kind ? flow : null;
    }
}