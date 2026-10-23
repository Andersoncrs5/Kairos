using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Identity.Users.Mapper;

[Mapper(AllowNullPropertyAssignment = false)]
public static partial class UserMapper
{
    public static UserModel ToModel(
        CreateUserDTO source,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new UserModel
        {
            FullName = source.FullName,
            UserName = source.UserName,
            Email = source.Email,
            PhoneNumber = source.PhoneNumber,
            Permissions = [],
            CreatedAt = createdAt
        };
    }

    public static partial UserDTO ToDto(UserModel source);

    public static partial void ApplyUpdate(
        UpdateUserDTO source,
        [MappingTarget] UserModel target);
}