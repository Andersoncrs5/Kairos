using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.Equipment.Mapper;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.Equipment.Controller;

[Route("api/v{version:apiVersion}/equipments")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class EquipmentController(
    IInboxTransactionUseCase<
        CreateEquipmentCommand,
        EquipmentModel,
        ICreateEquipmentUseCase> createEquipmentUseCase,

    IInboxTransactionUseCase<
        UpdateEquipmentCommand,
        EquipmentModel,
        IUpdateEquipmentUseCase> updateEquipmentUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IDeleteEquipmentByIdUseCase> deleteEquipmentUseCase,

    IInboxTransactionUseCase<
        string,
        EquipmentModel,
        IFindEquipmentByIdUseCase> findEquipmentByIdUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IExistsEquipmentByIdUseCase> existsEquipmentByIdUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IExistsEquipmentByCodeUseCase> existsEquipmentByCodeUseCase,

    IFindAllEquipmentUseCase findAllEquipmentUseCase
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of equipments.
    /// </summary>
    /// <remarks>
    /// Supports filtering by ID, version, creation date, update date,
    /// code, name, description and active status.
    /// Supports pagination and sort direction.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<Page<EquipmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Find(
        [FromQuery] FilterEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Page<EquipmentModel> result =
            await findAllEquipmentUseCase.ExecuteAsync(dto, ct);

        Page<EquipmentDto> data = ToPageDto(result);

        return SuccessResponse(
            data,
            "Equipments retrieved successfully",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves an equipment by its identifier.
    /// </summary>
    /// <remarks>
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpGet("{id}")]
    [RequirePermissions("equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<EquipmentModel> result =
            await findEquipmentByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            EquipmentMapper.ToDto,
            "Equipment found",
            "Error finding equipment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Exists by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether an equipment exists by its identifier.
    /// </summary>
    [HttpGet("Exists/id/{id}")]
    [RequirePermissions("equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExistsById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsEquipmentByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            value => (bool?)value,
            result.IsSuccess && result.Value
                ? "Equipment exists"
                : "Equipment does not exist",
            "Error checking equipment ID",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Exists by Code
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether an equipment exists by its code.
    /// </summary>
    [HttpGet("Exists/Code/{code}")]
    [RequirePermissions("equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExistsByCode(
        [FromRoute, Required] string code,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsEquipmentByCodeUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                code,
                ct);

        return ToResponse(
            result,
            value => (bool?)value,
            result.IsSuccess && result.Value
                ? "Equipment exists"
                : "Equipment does not exist",
            "Error checking equipment code",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new equipment.
    /// </summary>
    /// <remarks>
    /// Equipment codes and names must be unique.
    /// The authenticated user is used as the creator.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPost]
    [RequirePermissions("equipments.create")]
    [ProducesResponseType(
        typeof(ResponseHttp<EquipmentDto>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string requesterUserId = GetAuthenticatedUserId(inboxId);

        CreateEquipmentCommand command = new(dto, requesterUserId);

        Result<EquipmentModel> result =
            await createEquipmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            EquipmentMapper.ToDto,
            "Equipment created successfully",
            "Error creating equipment",
            inboxId,
            StatusCodes.Status201Created);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing equipment.
    /// </summary>
    /// <remarks>
    /// Equipment codes and names must be unique.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPut("{id}")]
    [RequirePermissions("equipments.update")]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string id,
        [FromBody] UpdateEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateEquipmentCommand command = new(id, dto);

        Result<EquipmentModel> result =
            await updateEquipmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            EquipmentMapper.ToDto,
            "Equipment updated successfully",
            "Error updating equipment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes an equipment by its identifier.
    /// </summary>
    /// <remarks>
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpDelete("{id}")]
    [RequirePermissions("equipments.delete")]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await deleteEquipmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            value => (bool?)value,
            "Equipment deleted successfully",
            "Error deleting equipment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static Page<EquipmentDto> ToPageDto(
        Page<EquipmentModel> page)
    {
        return Page<EquipmentDto>.Create(
            page.Data
                .Select(EquipmentMapper.ToDto)
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}
