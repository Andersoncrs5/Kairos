
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;

namespace Kairos.Api.Modules.Reservations;

public static class ReservationsModule
{
    public static IServiceCollection AddReservationsModule(
        this IServiceCollection services)
    {
        // ---------------------------------------------------------------
        // Repositories
        // ---------------------------------------------------------------

        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IReservationAttendeeRepository, ReservationAttendeeRepository>();

        // ---------------------------------------------------------------
        // Reservations
        // ---------------------------------------------------------------

        services.AddScoped<IFindReservationByIdUseCase, FindReservationByIdUseCase>();
        services.AddScoped<IFindReservationsUseCase, FindReservationsUseCase>();
        services.AddScoped<IExistsReservationByIdUseCase, ExistsReservationByIdUseCase>();
        services.AddScoped<IExistsConflictingByRoomUseCase, ExistsConflictingByRoomUseCase>();
        services.AddScoped<IGetConflictingByRoomUseCase, GetConflictingByRoomUseCase>();
        services.AddScoped<ICreateReservationUseCase, CreateReservationUseCase>();
        services.AddScoped<IUpdateReservationUseCase, UpdateReservationUseCase>();
        services.AddScoped<ICancelReservationUseCase, CancelReservationUseCase>();
        services.AddScoped<IDeleteReservationByIdUseCase, DeleteReservationByIdUseCase>();

        // ---------------------------------------------------------------
        // Reservation Attendees
        // ---------------------------------------------------------------

        services.AddScoped<IAddReservationAttendeeUseCase, AddReservationAttendeeUseCase>();
        services.AddScoped<ICheckInReservationAttendeeUseCase, CheckInReservationAttendeeUseCase>();
        services.AddScoped<IFindReservationAttendeeByIdUseCase, FindReservationAttendeeByIdUseCase>();
        services.AddScoped<IFindReservationAttendeesUseCase, FindReservationAttendeesUseCase>();
        services.AddScoped<IRemoveReservationAttendeeUseCase, RemoveReservationAttendeeUseCase>();
        services.AddScoped<IRespondReservationAttendeeUseCase, RespondReservationAttendeeUseCase>();
        services.AddScoped<IUpdateReservationAttendeeUseCase, UpdateReservationAttendeeUseCase>();

        return services;
    }
}
