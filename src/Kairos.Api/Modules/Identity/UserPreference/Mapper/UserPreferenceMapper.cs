using Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Requests;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Responses;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Identity.UserPreference.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false,
    EnumNamingStrategy = EnumNamingStrategy.CamelCase)]
public static partial class UserPreferenceMapper
{
    public static partial UserPreferenceModel ToModel(
        CreateUserPreferenceCommand source,
        string Id,
        long Version,
        DateTimeOffset CreatedAt);

    [MapperIgnoreSource(nameof(UserPreferenceModel.User))]
    public static partial UserPreferenceDTO ToDto(
        UserPreferenceModel source);

    public static partial void ApplyUpdate(
        UpdateUserPreferenceDTO source,
        [MappingTarget] UserPreferenceModel target);
}