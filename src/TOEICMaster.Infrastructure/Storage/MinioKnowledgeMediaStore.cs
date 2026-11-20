using Minio;
using Minio.DataModel.Args;
using TOEICMaster.Application.Knowledge.Admin;

namespace TOEICMaster.Infrastructure.Storage;

public sealed class MinioKnowledgeMediaStore(IMinioClient client) : IKnowledgeMediaStore
{
    private const string Bucket = "knowledge-images";

    public async Task UploadAsync(string objectKey, byte[] content, string contentType,
        CancellationToken cancellationToken)
    {
        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(Bucket), cancellationToken))
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(Bucket), cancellationToken);
        await using var stream = new MemoryStream(content, writable: false);
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(Bucket).WithObject(objectKey)
            .WithStreamData(stream).WithObjectSize(content.Length).WithContentType(contentType), cancellationToken);
    }

    public async Task<byte[]> DownloadAsync(string objectKey, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        await client.GetObjectAsync(new GetObjectArgs().WithBucket(Bucket).WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(output)), cancellationToken);
        return output.ToArray();
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) =>
        client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(Bucket).WithObject(objectKey), cancellationToken);
}
