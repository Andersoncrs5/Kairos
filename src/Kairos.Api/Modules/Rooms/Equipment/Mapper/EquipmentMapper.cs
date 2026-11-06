using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.Equipment.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class EquipmentMapper
{
    public static partial EquipmentModel ToModel(
        CreateEquipmentDto source,
        string id,
        DateTimeOffset createdAt);

    public static partial EquipmentDto ToDto(
        EquipmentModel source);

    public static partial void ApplyUpdate(
        UpdateEquipmentDto source,
        [MappingTarget] EquipmentModel target);
}