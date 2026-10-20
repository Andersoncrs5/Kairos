using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;

namespace Kairos.Api.Modules.Rooms;

public static class RoomsModule
{
    public static IServiceCollection AddRoomsModule(this IServiceCollection services)
    {
        services.AddScoped<IRoomTypeRepository, RoomTypeRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IRoomBlockoutRepository, RoomBlockoutRepository>();
        
        return services;
    }
}