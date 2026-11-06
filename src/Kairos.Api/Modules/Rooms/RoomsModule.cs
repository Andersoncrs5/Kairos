using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;
using Kairos.Api.Modules.Rooms.Equipment.Services.Providers;
using Microsoft.Extensions.DependencyInjection;

using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;
using Kairos.Api.Modules.Rooms.Room.Services.Providers;

using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;

namespace Kairos.Api.Modules.Rooms;

public static class RoomsModule
{
    public static IServiceCollection AddRoomsModule(
        this IServiceCollection services)
    {
        // ---------------------------------------------------------------
        // Repositories
        // ---------------------------------------------------------------
        
        services.AddScoped<IRoomEquipmentRepository, RoomEquipmentRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IRoomTypeRepository, RoomTypeRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IRoomBlockoutRepository, RoomBlockoutRepository>();
        services.AddScoped<IRoomOperatingHourRepository, RoomOperatingHourRepository>();

        // ---------------------------------------------------------------
        // RoomType use cases
        // ---------------------------------------------------------------

        services.AddScoped<ICreateRoomTypeUseCase, CreateRoomTypeUseCase>();
        services.AddScoped<IUpdateRoomTypeUseCase, UpdateRoomTypeUseCase>();
        services.AddScoped<IDeleteRoomTypeUseCase, DeleteRoomTypeUseCase>();
        services.AddScoped<IFindRoomTypeUseCase, FindRoomTypeUseCase>();
        services.AddScoped<IFindAllRoomTypeUseCase, FindAllRoomTypeUseCase>();
        services.AddScoped<IExistsRoomTypeByIdUseCase, ExistsRoomTypeByIdUseCase>();
        services.AddScoped<IExistsRoomTypeByNameUseCase, ExistsRoomTypeByNameUseCase>();

        // ---------------------------------------------------------------
        // Room use cases
        // ---------------------------------------------------------------

        services.AddScoped<IDeleteRoomByIdUseCase, DeleteRoomByIdUseCase>();
        services.AddScoped<IFindAllRoomUseCase, FindAllRoomUseCase>();
        services.AddScoped<IFindRoomByIdUseCase, FindRoomByIdUseCase>();
        services.AddScoped<ICreateRoomUseCase, CreateRoomUseCase>();
        services.AddScoped<IUpdateRoomUseCase, UpdateRoomUseCase>();
        services.AddScoped<IExistsRoomByCodeUseCase, ExistsRoomByCodeUseCase>();
        services.AddScoped<IExistsRoomByIdUseCase, ExistsRoomByIdUseCase>();

        // ---------------------------------------------------------------
        // RoomBlockout use cases
        // ---------------------------------------------------------------

        services.AddScoped<ICreateRoomBlockoutUseCase, CreateRoomBlockoutUseCase>();
        services.AddScoped<IDeleteRoomBlockoutByIdCase, DeleteRoomBlockoutByIdCase>();
        services.AddScoped<IExistsRoomBlockoutByIdUseCase, ExistsRoomBlockoutByIdUseCase>();
        services.AddScoped<IFindAllRoomBlockoutUseCase, FindAllRoomBlockoutUseCase>();
        services.AddScoped<IFindRoomBlockoutUseCase, FindRoomBlockoutUseCase>();
        services.AddScoped<IUpdateRoomBlockoutUseCase, UpdateRoomBlockoutUseCase>();
        services.AddScoped<ICancelRoomBlockoutUseCase, CancelRoomBlockoutUseCase>();

        // ---------------------------------------------------------------
        // RoomOperatingHour use cases
        // ---------------------------------------------------------------

        services.AddScoped<ICreateRoomOperatingHourUseCase, CreateRoomOperatingHourUseCase>();
        services.AddScoped<IDeleteRoomOperatingHourUseCase, DeleteRoomOperatingHourUseCase>();
        services.AddScoped<IExistsRoomOperatingHourUseCase, ExistsRoomOperatingHourUseCase>();
        services.AddScoped<IFindRoomOperatingHourByIdUseCase, FindRoomOperatingHourByIdUseCase>();
        services.AddScoped<IFindRoomOperatingHoursByRoomIdUseCase, FindRoomOperatingHoursByRoomIdUseCase>();
        services.AddScoped<IFindRoomOperatingHourService, FindRoomOperatingHourService>();
        services.AddScoped<IUpdateRoomOperatingHourUseCase, UpdateRoomOperatingHourUseCase>();
        
        // ---------------------------------------------------------------
        // Equipment use cases
        // ---------------------------------------------------------------

        services.AddScoped<ICreateEquipmentUseCase, CreateEquipmentUseCase>();
        services.AddScoped<IUpdateEquipmentUseCase, UpdateEquipmentUseCase>();
        services.AddScoped<IDeleteEquipmentByIdUseCase, DeleteEquipmentByIdUseCase>();
        services.AddScoped<IFindEquipmentByIdUseCase, FindEquipmentByIdUseCase>();
        services.AddScoped<IFindAllEquipmentUseCase, FindAllEquipmentUseCase>();
        services.AddScoped<IExistsEquipmentByIdUseCase, ExistsEquipmentByIdUseCase>();
        services.AddScoped<IExistsEquipmentByCodeUseCase, ExistsEquipmentByCodeUseCase>();
        
        // ---------------------------------------------------------------
        // RoomEquipment use cases
        // ---------------------------------------------------------------

        services.AddScoped<ICreateRoomEquipmentUseCase, CreateRoomEquipmentUseCase>();
        services.AddScoped<IUpdateRoomEquipmentUseCase, UpdateRoomEquipmentUseCase>();
        services.AddScoped<IDeleteRoomEquipmentUseCase, DeleteRoomEquipmentUseCase>();
        services.AddScoped<IFindRoomEquipmentByIdUseCase, FindRoomEquipmentByIdUseCase>();
        services.AddScoped<IFindRoomEquipmentsUseCase, FindRoomEquipmentsUseCase>();
        services.AddScoped<IFindRoomEquipmentByRoomAndEquipmentUseCase, FindRoomEquipmentByRoomAndEquipmentUseCase>();
        services.AddScoped<IExistsRoomEquipmentByRoomAndEquipmentUseCase, ExistsRoomEquipmentByRoomAndEquipmentUseCase>();
        services.AddScoped<IFindRoomEquipmentsByRoomIdUseCase, FindRoomEquipmentsByRoomIdUseCase>();
        
        return services;
    }
}