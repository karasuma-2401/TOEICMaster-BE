namespace TOEICMaster.Application.Exams;

public interface IExamMediaStore
{
    Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
