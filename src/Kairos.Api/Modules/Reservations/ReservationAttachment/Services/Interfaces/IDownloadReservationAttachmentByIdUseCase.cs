using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

public interface IDownloadReservationAttachmentByIdUseCase
    : IUseCase<DownloadReservationAttachmentByIdCommand, Result<FileDownload>>
{
}