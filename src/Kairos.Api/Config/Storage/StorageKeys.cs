using System.Text.RegularExpressions;

namespace Kairos.Api.Config.Storage;

public static partial class StorageKeys
{
    // Os IDs do projeto são GUIDs em minúsculas
    public static string RoomPhotos(string roomId) => $"rooms/{roomId}/photos";

    public static string RoomFloorPlans(string roomId) => $"rooms/{roomId}/floor-plans";

    public static string ReservationMaterials(string reservationId)
        => $"reservations/{reservationId}/materials";

    public static bool IsValidPrefix(string? prefix) => IsValid(prefix, allowTrailingSlash: true);

    public static bool IsValidKey(string? key) => IsValid(key, allowTrailingSlash: false);

    private static bool IsValid(string? value, bool allowTrailingSlash)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024)
        {
            return false;
        }

        if (!allowTrailingSlash && value.EndsWith('/'))
        {
            return false;
        }

        return SafePathRegex.IsMatch(value);
    }

    private static readonly Regex SafePathRegex = new(
        @"^(?!.*\.\.)(?!.*//)[a-z0-9][a-z0-9/_.\-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
}