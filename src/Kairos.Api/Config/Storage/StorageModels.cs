namespace Kairos.Api.Config.Storage;

public enum StorageBucket
{
    RoomMedia,
    SupportMaterials
}

/// <summary>
/// O tipo de upload define bucket, tipos permitidos e tamanho máximo.
/// O chamador não escolhe nenhum deles.
/// </summary>
public enum UploadKind
{
    RoomPhoto,
    RoomFloorPlan,
    SupportMaterial
}

public sealed record UploadFileRequest(
    UploadKind Kind,
    string KeyPrefix,   
    Stream Content);

public sealed record StoredFile(
    StorageBucket Bucket,
    string Key,
    string ContentType,   
    long SizeBytes,
    string Sha256,
    string? ETag);

public sealed record StoredFileInfo(
    string Key,
    string? ContentType,  
    long SizeBytes,
    string? ETag,
    DateTimeOffset LastModified);

public sealed record PresignedUrl(string Url, DateTimeOffset ExpiresAt);

public sealed record DownloadUrlOptions(
    TimeSpan? ExpiresIn = null,
    string? FileName = null,   
    bool Inline = false);      