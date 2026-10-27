using Minio;
using Minio.DataModel.Args;
using TOEICMaster.Application.Exams;

namespace TOEICMaster.Infrastructure.Storage;

public sealed class MinioExamMediaStore(IMinioClient client) : IExamMediaStore
{
    private const string Bucket = "exam-media";

    public async Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(Bucket), cancellationToken))
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(Bucket), cancellationToken);
        await using var stream = new MemoryStream(content, writable: false);
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(Bucket).WithObject(objectKey).WithStreamData(stream)
            .WithObjectSize(content.Length).WithContentType(contentType), cancellationToken);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => client.RemoveObjectAsync(
        new RemoveObjectArgs().WithBucket(Bucket).WithObject(objectKey), cancellationToken);
}
