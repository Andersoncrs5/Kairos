
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;
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
        services.AddScoped<IReservationAttachmentRepository, ReservationAttachmentRepository>();

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

        // ---------------------------------------------------------------
        // Reservation Attachments
        // ---------------------------------------------------------------
        
        services.AddScoped<IFindReservationAttachmentUseCase, FindReservationAttachmentUseCase>();
        services.AddScoped<IFindReservationAttachmentByIdUseCase, FindReservationAttachmentByIdUseCase>();
        services.AddScoped<IDownloadReservationAttachmentByIdUseCase, DownloadReservationAttachmentByIdUseCase>();
        services.AddScoped<IDownloadReservationAttachmentByStorageKeyUseCase, DownloadReservationAttachmentByStorageKeyUseCase>();
        services.AddScoped<IUpdateReservationAttachmentUseCase, UpdateReservationAttachmentUseCase>();
        services.AddScoped<ICreateReservationAttachmentUseCase, CreateReservationAttachmentUseCase>();
        services.AddScoped<IDeleteReservationAttachmentUseCase, DeleteReservationAttachmentUseCase>();
        
        return services;
    }
}
