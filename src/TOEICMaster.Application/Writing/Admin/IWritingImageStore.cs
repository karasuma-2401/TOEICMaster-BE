namespace TOEICMaster.Application.Writing.Admin;

public interface IWritingImageStore
{
    Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken);
    Task<WritingImageContent> DownloadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed record WritingImageContent(byte[] Data, string ContentType);
