using Minio;
using Minio.DataModel.Args;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Storage;

public sealed class MinioSpeakingMediaStore(IMinioClient client) : ISpeakingMediaStore
{
    public async Task UploadAsync(SpeakingMediaKind kind, string objectKey, byte[] content, string contentType,
        CancellationToken cancellationToken)
    {
        var bucket = Bucket(kind);
        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken))
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);
        await using var stream = new MemoryStream(content, writable: false);
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(objectKey)
            .WithStreamData(stream).WithObjectSize(content.Length).WithContentType(contentType), cancellationToken);
    }

    public async Task<byte[]> DownloadAsync(SpeakingMediaKind kind, string objectKey,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        await client.GetObjectAsync(new GetObjectArgs().WithBucket(Bucket(kind)).WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(output)), cancellationToken);
        return output.ToArray();
    }

    public Task DeleteAsync(SpeakingMediaKind kind, string objectKey, CancellationToken cancellationToken) =>
        client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(Bucket(kind)).WithObject(objectKey),
            cancellationToken);

    private static string Bucket(SpeakingMediaKind kind) => kind switch
    {
        SpeakingMediaKind.Image => "speaking-media",
        SpeakingMediaKind.Recording => "speaking-recordings",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
