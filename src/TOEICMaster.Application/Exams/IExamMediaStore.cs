namespace TOEICMaster.Application.Exams;

public sealed record ExamMediaObjectMetadata(long ByteSize, string ContentType, string? ETag);

public interface IExamMediaStore
{
    Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
    Task<string> CreatePresignedPutUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    Task<ExamMediaObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
