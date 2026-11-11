namespace Kairos.Api.Config.Storage.Contracts;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public bool Enabled { get; set; } = true;

    public string ServiceUrl { get; set; } = "http://localhost:9000";

    public string? PublicUrl { get; set; }

    public string Region { get; set; } = "us-east-1";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool ForcePathStyle { get; set; } = true;

    public bool EnsureBucketsOnStartup { get; set; } = true;

    public int MaxErrorRetry { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 60;

    public int PresignedUrlExpirationMinutes { get; set; } = 15;

    public StorageBucketsOptions Buckets { get; set; } = new();

    public StorageLimitsOptions Limits { get; set; } = new();
}

public sealed class StorageBucketsOptions
{
    public string RoomMedia { get; set; } = "kairos-room-media";

    public string SupportMaterials { get; set; } = "kairos-support-materials";

    public IEnumerable<string> All => [RoomMedia, SupportMaterials];
}

public sealed class StorageLimitsOptions
{
    public long MaxPhotoBytes { get; set; } = 5 * 1024 * 1024;

    public long MaxFloorPlanBytes { get; set; } = 10 * 1024 * 1024;

    public long MaxSupportMaterialBytes { get; set; } = 25 * 1024 * 1024;
}