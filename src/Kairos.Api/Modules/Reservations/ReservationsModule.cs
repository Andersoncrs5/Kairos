using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

namespace Kairos.Api.Modules.Reservations;

public static class ReservationsModule
{
    public static IServiceCollection AddReservationsModule(
        this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IReservationRepository, ReservationRepository>();

        // Queries
        services.AddScoped<IFindReservationByIdUseCase, FindReservationByIdUseCase>();
        services.AddScoped<IFindReservationsUseCase, FindReservationsUseCase>();
        services.AddScoped<IExistsReservationByIdUseCase, ExistsReservationByIdUseCase>();
        services.AddScoped<IExistsConflictingByRoomUseCase, ExistsConflictingByRoomUseCase>();
        services.AddScoped<IGetConflictingByRoomUseCase, GetConflictingByRoomUseCase>();

        // Commands
        services.AddScoped<ICreateReservationUseCase, CreateReservationUseCase>();
        services.AddScoped<IUpdateReservationUseCase, UpdateReservationUseCase>();
        services.AddScoped<ICancelReservationUseCase, CancelReservationUseCase>();
        services.AddScoped<IDeleteReservationByIdUseCase, DeleteReservationByIdUseCase>();

        return services;
    }
}