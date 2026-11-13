using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using TOEICMaster.Application.Exams;

namespace TOEICMaster.Infrastructure.Storage;

public sealed class MinioExamMediaStore(IMinioClient client, IOptions<MinioOptions> options) : IExamMediaStore
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

    public async Task<string> CreatePresignedPutUrlAsync(string objectKey, TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        var settings = options.Value;
        var publicClient = new MinioClient().WithEndpoint(settings.PublicEndpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey).WithSSL(settings.UseSsl).Build();
        return await publicClient.PresignedPutObjectAsync(new PresignedPutObjectArgs().WithBucket(Bucket)
            .WithObject(objectKey).WithExpiry((int)lifetime.TotalSeconds));
    }

    public async Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        var settings = options.Value;
        var publicClient = new MinioClient().WithEndpoint(settings.PublicEndpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey).WithSSL(settings.UseSsl).Build();
        return await publicClient.PresignedGetObjectAsync(new PresignedGetObjectArgs().WithBucket(Bucket)
            .WithObject(objectKey).WithExpiry((int)lifetime.TotalSeconds));
    }

    public async Task<ExamMediaObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await client.StatObjectAsync(new StatObjectArgs().WithBucket(Bucket).WithObject(objectKey),
                cancellationToken);
            return new ExamMediaObjectMetadata(metadata.Size, metadata.ContentType, metadata.ETag);
        }
        catch (Minio.Exceptions.MinioException)
        {
            return null;
        }
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(Bucket), cancellationToken))
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(Bucket), cancellationToken);
    }
}
