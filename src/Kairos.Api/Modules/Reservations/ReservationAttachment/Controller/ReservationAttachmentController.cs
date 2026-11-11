using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Config.Storage;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Reservations.Reservation.Mapper;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Mapper;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Controller;

[Route("api/v{version:apiVersion}/reservations")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class ReservationAttachmentController(
    IInboxTransactionUseCase<FilterReservationAttachmentsCommand, Page<ReservationAttachmentModel>, IFindReservationAttachmentUseCase> findAttachmentsUseCase,
    IInboxTransactionUseCase<FindReservationAttachmentByIdCommand, ReservationAttachmentDto, IFindReservationAttachmentByIdUseCase> findAttachmentByIdUseCase,
    IInboxTransactionUseCase<DownloadReservationAttachmentByIdCommand, FileDownload, IDownloadReservationAttachmentByIdUseCase> downloadByIdUseCase,
    IInboxTransactionUseCase<DownloadReservationAttachmentByStorageKeyCommand, FileDownload, IDownloadReservationAttachmentByStorageKeyUseCase> downloadByStorageKeyUseCase,
    IInboxTransactionUseCase<CreateReservationAttachmentCommand, ReservationAttachmentModel, ICreateReservationAttachmentUseCase> createAttachmentUseCase,
    IInboxTransactionUseCase<UpdateReservationAttachmentCommand, ReservationAttachmentModel, IUpdateReservationAttachmentUseCase> updateAttachmentUseCase,
    IInboxTransactionUseCase<DeleteReservationAttachmentCommand, object, IDeleteReservationAttachmentUseCase> deleteAttachmentUseCase
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Find attachments of a reservation
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of the attachments of a reservation.
    /// </summary>
    /// <remarks>
    /// The reservation is taken from the route. Supports filtering,
    /// pagination and sorting. The list does not include the download URL;
    /// use the find by ID endpoint to get it.
    /// </remarks>
    [HttpGet("{reservationId}/attachments")]
    [RequirePermissions("reservations.attachments.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<ReservationAttachmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Find(
        [FromRoute, Required] string reservationId,
        [FromQuery] FilterReservationAttachmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string requesterUserId = GetAuthenticatedUserId(inboxId);

        // The route always wins over any "reservationId" sent in the query
        dto.ReservationId = reservationId;

        Result<Page<ReservationAttachmentModel>> result =
            await findAttachmentsUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                new FilterReservationAttachmentsCommand(requesterUserId, dto),
                ct);

        return ToResponse(
            result,
            ToPageDto,
            "Reservation attachments retrieved successfully",
            "Error retrieving reservation attachments",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a reservation attachment by its identifier.
    /// </summary>
    /// <remarks>
    /// The response includes a temporary pre-signed download URL.
    /// </remarks>
    [HttpGet("attachments/{attachmentId:guid}")]
    [RequirePermissions("reservations.attachments.read")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationAttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string attachmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        FindReservationAttachmentByIdCommand command = new(
            AttachmentId: attachmentId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<ReservationAttachmentDto> result = await findAttachmentByIdUseCase.ExecuteAsync(inboxId, NewWorkerId(), command, ct);

        return ToResponse(
            result,
            attachment => attachment,
            "Reservation attachment found",
            "Error finding reservation attachment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Download by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Downloads the file of a reservation attachment.
    /// </summary>
    /// <remarks>
    /// The file is streamed from the storage. Like every route under /api,
    /// it requires the X-Inbox-Id header.
    /// </remarks>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    [RequirePermissions("reservations.attachments.download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadById(
        [FromRoute, Required] string attachmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        DownloadReservationAttachmentByIdCommand command = new(
            AttachmentId: attachmentId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<FileDownload> result =
            await downloadByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToFileResponse(
            result,
            "Error downloading reservation attachment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Download by storage key
    // ---------------------------------------------------------------

    /// <summary>
    /// Downloads the file of a reservation attachment by its storage key.
    /// </summary>
    /// <remarks>
    /// The storage key is sent in the query string because it contains slashes.
    /// </remarks>
    [HttpGet("attachments/download")]
    [RequirePermissions("reservations.attachments.download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadByStorageKey(
        [FromQuery, Required] string storageKey,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        DownloadReservationAttachmentByStorageKeyCommand command = new(
            StorageKey: storageKey,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<FileDownload> result =
            await downloadByStorageKeyUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToFileResponse(
            result,
            "Error downloading reservation attachment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Create (upload)
    // ---------------------------------------------------------------

    /// <summary>
    /// Uploads an attachment to a reservation.
    /// </summary>
    /// <remarks>
    /// Send the request as multipart/form-data with the fields File,
    /// DisplayName, Description (optional) and Visibility (optional,
    /// Participants by default).
    /// </remarks>
    [HttpPost("{reservationId}/attachments")]
    [Consumes("multipart/form-data")]
    [RequirePermissions("reservations.attachments.create")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationAttachmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromRoute, Required] string reservationId,
        [FromForm] CreateReservationAttachmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        CreateReservationAttachmentCommand command = new(
            ReservationId: reservationId,
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<ReservationAttachmentModel> result =
            await createAttachmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            attachment => ReservationAttachmentMapper.ToDto(attachment),
            "Reservation attachment created successfully",
            "Error creating reservation attachment",
            inboxId,
            StatusCodes.Status201Created);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates the display name, description and visibility of an attachment.
    /// </summary>
    /// <remarks>
    /// The file itself cannot be replaced; delete the attachment and upload
    /// a new one instead.
    /// </remarks>
    [HttpPut("attachments/{attachmentId:guid}")]
    [RequirePermissions("reservations.attachments.update")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationAttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string attachmentId,
        [FromBody] UpdateReservationAttachmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateReservationAttachmentCommand command = new(
            AttachmentId: attachmentId,
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<ReservationAttachmentModel> result =
            await updateAttachmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            attachment => ReservationAttachmentMapper.ToDto(attachment),
            "Reservation attachment updated successfully",
            "Error updating reservation attachment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a reservation attachment and its file from the storage.
    /// </summary>
    [HttpDelete("attachments/{attachmentId:guid}")]
    [RequirePermissions("reservations.attachments.delete")]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute, Required] string attachmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        DeleteReservationAttachmentCommand command = new(
            AttachmentId: attachmentId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<object> result =
            await deleteAttachmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        // The delete use case returns Result<object>.Ok(200), whose Value is null.
        // ToResponse treats a null value as failure, so this one is handled here.
        if (result.IsFailure)
        {
            return FailureResponse<object>(
                result.StatusCode,
                result.Errors,
                "Error deleting reservation attachment",
                inboxId);
        }

        return SuccessResponse<object>(
            null,
            "Reservation attachment deleted successfully",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private IActionResult ToFileResponse(
        Result<FileDownload> result,
        string errorMessage,
        string inboxId)
    {
        if (result.IsFailure || result.Value is null)
        {
            return FailureResponse<object>(
                result.StatusCode,
                result.Errors,
                errorMessage,
                inboxId);
        }

        FileDownload download = result.Value;

        // Releases the S3 response when the HTTP request finishes
        Response.RegisterForDispose(download);

        // Forces the browser to download instead of rendering user content
        Response.Headers[HeaderNames.ContentDisposition] = "attachment";

        return File(download.Content, download.ContentType);
    }

    private static Page<ReservationAttachmentDto> ToPageDto(
        Page<ReservationAttachmentModel> page)
    {
        return Page<ReservationAttachmentDto>.Create(
            page.Data
                .Select(model => ReservationAttachmentMapper.ToDto(
                    model,
                    reservation: model.Reservation is null
                        ? null
                        : ReservationMapper.ToDto(model.Reservation),
                    uploader: model.Uploader is null
                        ? null
                        : UserMapper.ToDto(model.Uploader)))
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}