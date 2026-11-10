using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;

namespace Kairos.Api.Config.RateLimiting;

public static class RateLimitPartitionKeys
{
    public static string UserOrIp(HttpContext context)
        => TryGetUserId(context) is { } id ? $"user:{id}" : Ip(context);

    public static string Ip(HttpContext context)
        => $"ip:{NormalizeIp(context.Connection.RemoteIpAddress)}";

    private static string? TryGetUserId(HttpContext context)
    {
        ClaimsPrincipal user = context.User;

        if (user.Identity?.IsAuthenticated != true)
            return null;

        string? id =
            user.FindFirstValue(JwtRegisteredClaimNames.Sid)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }

    private static string NormalizeIp(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            Span<byte> bytes = stackalloc byte[16];

            if (address.TryWriteBytes(bytes, out _))
            {
                bytes[8..].Clear();
                return $"{new IPAddress(bytes)}/64";
            }
        }

        return address.ToString();
    }
}