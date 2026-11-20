using Kairos.Api.Modules.Identity.Passkeys.DTOs.Response;
using Kairos.Api.Modules.Identity.Passkeys.Models;

namespace Kairos.Api.Modules.Identity.Passkeys.Mapper;

public static class PasskeyMapper
{
    public static PasskeyDTO ToDto(UserPasskeyModel passkey) => new()
    {
        Id = passkey.Id,
        Name = passkey.Name,
        CreatedAt = passkey.CreatedAt,
        LastUsedAt = passkey.LastUsedAt,
        IsBackupEligible = passkey.IsBackupEligible,
        IsBackedUp = passkey.IsBackedUp
    };
}