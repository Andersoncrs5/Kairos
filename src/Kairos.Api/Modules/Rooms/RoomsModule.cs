using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;
using Kairos.Api.Modules.Rooms.Room.Services.Providers;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

namespace Kairos.Api.Modules.Rooms;

public static class RoomsModule
{
    public static IServiceCollection AddRoomsModule(
        this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IRoomTypeRepository, RoomTypeRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IRoomBlockoutRepository, RoomBlockoutRepository>();

        // RoomType use cases
        services.AddScoped<ICreateRoomTypeUseCase, CreateRoomTypeUseCase>();
        services.AddScoped<IUpdateRoomTypeUseCase, UpdateRoomTypeUseCase>();
        services.AddScoped<IDeleteRoomTypeUseCase, DeleteRoomTypeUseCase>();
        services.AddScoped<IFindRoomTypeUseCase, FindRoomTypeUseCase>();
        services.AddScoped<IFindAllRoomTypeUseCase, FindAllRoomTypeUseCase>();
        services.AddScoped<IExistsRoomTypeByIdUseCase, ExistsRoomTypeByIdUseCase>();
        services.AddScoped<IExistsRoomTypeByNameUseCase, ExistsRoomTypeByNameUseCase>();
        
        services.AddScoped<IDeleteRoomByIdUseCase, DeleteRoomByIdUseCase>();
        services.AddScoped<IFindAllRoomUseCase, FindAllRoomUseCase>();
        services.AddScoped<IFindRoomByIdUseCase, FindRoomByIdUseCase>();
        services.AddScoped<ICreateRoomUseCase, CreateRoomUseCase>();
        services.AddScoped<IUpdateRoomUseCase, UpdateRoomUseCase>();
        services.AddScoped<IExistsRoomByCodeUseCase, ExistsRoomByCodeUseCase>();
        services.AddScoped<IExistsRoomByIdUseCase, ExistsRoomByIdUseCase>();
        
        return services;
    }
}