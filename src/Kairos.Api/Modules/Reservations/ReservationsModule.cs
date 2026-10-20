using Kairos.Api.Modules.Reservations.Reservation.Repositories;

namespace Kairos.Api.Modules.Reservations;

public static class ReservationsModule
{
    public static IServiceCollection AddReservationsModule(this IServiceCollection services)
    {
        services.AddScoped<IReservationRepository, ReservationRepository>();
        
        return services;
    }
}