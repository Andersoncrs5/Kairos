using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.Room.DTOs.Request;

public sealed class UpdateRoomDto
{
    [StringLength(
        36,
        ErrorMessage = "Room type ID cannot exceed 36 characters.")]
    public string? RoomTypeId { get; init; }

    [StringLength(
        30,
        ErrorMessage = "Room code cannot exceed 30 characters.")]
    public string? Code { get; init; }

    [StringLength(
        120,
        ErrorMessage = "Room name cannot exceed 120 characters.")]
    public string? Name { get; init; }

    [StringLength(
        500,
        ErrorMessage = "Room description cannot exceed 500 characters.")]
    public string? Description { get; init; }

    [StringLength(
        150,
        ErrorMessage = "Room location cannot exceed 150 characters.")]
    public string? Location { get; init; }

    [StringLength(
        30,
        ErrorMessage = "Room floor cannot exceed 30 characters.")]
    public string? Floor { get; init; }

    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Room capacity must be greater than zero.")]
    public int? Capacity { get; init; }

    [Range(
        typeof(decimal),
        "0.01",
        "99999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Room area must be greater than zero and cannot exceed 99999999.99.")]
    public decimal? AreaSquareMeters { get; init; }

    public bool? IsAccessible { get; init; }

    public bool? IsReservable { get; init; }

    public bool? IsActive { get; init; }
}