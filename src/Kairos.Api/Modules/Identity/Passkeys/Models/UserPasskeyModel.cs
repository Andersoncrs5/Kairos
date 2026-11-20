using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Passkeys.Models;

public sealed class UserPasskeyModel : BaseModel
{
    public required string UserId { get; set; }

    public UserModel? User { get; set; }

    public required byte[] CredentialId { get; set; }

    public required byte[] PublicKey { get; set; }

    public long SignCount { get; set; }

    public required string Name { get; set; }

    public bool IsBackupEligible { get; set; }

    public bool IsBackedUp { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}