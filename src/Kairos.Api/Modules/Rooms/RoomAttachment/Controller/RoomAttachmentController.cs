
using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Config.Storage;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomAttachment.Mappers;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Controller;

[Route("api/v{version:apiVersion}/rooms")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomAttachmentController(
    IInboxTransactionUseCase<
        FilterRoomAttachmentsCommand,
        Page<RoomAttachmentModel>,
        IFindRoomAttachmentUseCase> findAttachmentsUseCase,

    IInboxTransactionUseCase<
        FindRoomAttachmentByIdCommand,
        RoomAttachmentModel,
        IFindRoomAttachmentByIdUseCase> findAttachmentByIdUseCase,

    IInboxTransactionUseCase<
        DownloadRoomAttachmentByIdCommand,
        FileDownload,
        IDownloadRoomAttachmentByIdUseCase> downloadByIdUseCase,

    IInboxTransactionUseCase<
        DownloadRoomAttachmentByStorageKeyCommand,
        FileDownload,
        IDownloadRoomAttachmentByStorageKeyUseCase> downloadByStorageKeyUseCase,

    IInboxTransactionUseCase<
        UpdateRoomAttachmentCommand,
        RoomAttachmentModel,
        IUpdateRoomAttachmentUseCase> updateAttachmentUseCase,

    IInboxTransactionUseCase<
        DeleteRoomAttachmentCommand,
        object,
        IDeleteRoomAttachmentUseCase> deleteAttachmentUseCase
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Find attachments of a room
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of the attachments of a room.
    /// </summary>
    /// <remarks>
    /// The room is taken from the route. Supports filtering,
    /// pagination and sorting. The list does not include download URLs;
    /// use the find by ID endpoint to retrieve an individual attachment.
    /// </remarks>
    [HttpGet("{roomId}/attachments")]
    [RequirePermissions("rooms.attachments.list")]
    [ProducesResponseType(
        typeof(ResponseHttp<Page<RoomAttachmentDto>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Find(
        [FromRoute, Required] string roomId,
        [FromQuery] FilterRoomAttachmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string requesterUserId = GetAuthenticatedUserId(inboxId);

        // The route always wins over any RoomId sent in the query.
        dto.RoomId = roomId;

        Result<Page<RoomAttachmentModel>> result =
            await findAttachmentsUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                new FilterRoomAttachmentsCommand(requesterUserId, dto),
                ct);

        return ToResponse(
            result,
            ToPageDto,
            "Room attachments retrieved successfully",
            "Error retrieving room attachments",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room attachment by its identifier.
    /// </summary>
    /// <remarks>
    /// The response does not expose the internal storage key.
    /// </remarks>
    [HttpGet("attachments/{attachmentId:guid}")]
    [RequirePermissions("rooms.attachments.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomAttachmentDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string attachmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        FindRoomAttachmentByIdCommand command = new(
            AttachmentId: attachmentId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<RoomAttachmentModel> result =
            await findAttachmentByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            attachment => RoomAttachmentMapper.ToDto(attachment),
            "Room attachment found",
            "Error finding room attachment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Download by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Downloads the file of a room attachment.
    /// </summary>
    /// <remarks>
    /// The file is streamed from the storage.
    /// Requires the X-Inbox-Id header.
    /// </remarks>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    [RequirePermissions("rooms.attachments.download")]
    [ProducesResponseType(
        typeof(FileResult),
        StatusCodes.Status200OK,
        "application/octet-stream")]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadById(
        [FromRoute, Required] string attachmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        DownloadRoomAttachmentByIdCommand command = new(
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
            "Error downloading room attachment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Download by storage key
    // ---------------------------------------------------------------

    /// <summary>
    /// Downloads the file of a room attachment by its storage key.
    /// </summary>
    /// <remarks>
    /// The storage key is sent in the query string because it may contain
    /// slashes. The use case must resolve the key through the attachment
    /// repository before accessing the storage.
    /// </remarks>
    [HttpGet("attachments/download")]
    [RequirePermissions("rooms.attachments.download")]
    [ProducesResponseType(
        typeof(FileResult),
        StatusCodes.Status200OK,
        "application/octet-stream")]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadByStorageKey(
        [FromQuery, Required] string storageKey,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        DownloadRoomAttachmentByStorageKeyCommand command = new(
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
            "Error downloading room attachment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates the metadata of a room attachment.
    /// </summary>
    /// <remarks>
    /// Updates the alt text, sort order and cover status.
    /// The file itself cannot be replaced; delete the attachment and
    /// upload a new one instead.
    /// </remarks>
    [HttpPut("attachments/{attachmentId:guid}")]
    [RequirePermissions("rooms.attachments.update")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomAttachmentDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string attachmentId,
        [FromBody] UpdateRoomAttachmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateRoomAttachmentCommand command = new(
            AttachmentId: attachmentId,
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<RoomAttachmentModel> result =
            await updateAttachmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            attachment => RoomAttachmentMapper.ToDto(attachment),
            "Room attachment updated successfully",
            "Error updating room attachment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room attachment and its file from the storage.
    /// </summary>
    [HttpDelete("attachments/{attachmentId:guid}")]
    [RequirePermissions("rooms.attachments.delete")]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute, Required] string attachmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        DeleteRoomAttachmentCommand command = new(
            AttachmentId: attachmentId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<object> result =
            await deleteAttachmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        // The delete use case returns Result<object>.Ok(200),
        // whose Value is null. ToResponse treats a null value as failure.
        if (result.IsFailure)
        {
            return FailureResponse<object>(
                result.StatusCode,
                result.Errors,
                "Error deleting room attachment",
                inboxId);
        }

        return SuccessResponse<object>(
            null,
            "Room attachment deleted successfully",
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

        // Releases the S3 response when the HTTP request finishes.
        Response.RegisterForDispose(download);

        // Forces the browser to download instead of rendering user content.
        Response.Headers[HeaderNames.ContentDisposition] = "attachment";

        return File(download.Content, download.ContentType);
    }

    private static Page<RoomAttachmentDto> ToPageDto(
        Page<RoomAttachmentModel> page)
    {
        return Page<RoomAttachmentDto>.Create(
            page.Data
                .Select(model => RoomAttachmentMapper.ToDto(model))
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}
