namespace TOEICMaster.Application.Profile;

public interface IAvatarStore
{
    Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken);
    Task<AvatarContent> DownloadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed record AvatarContent(byte[] Data, string ContentType);
