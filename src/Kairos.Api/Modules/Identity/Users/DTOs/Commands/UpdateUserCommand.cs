using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;

namespace Kairos.Api.Modules.Identity.Users.DTOs.Commands;

public sealed record UpdateUserCommand(
    [property: Required(ErrorMessage = "User ID is required.")]
    string UserId,

    [property: Required]
    UpdateUserDTO Dto
);