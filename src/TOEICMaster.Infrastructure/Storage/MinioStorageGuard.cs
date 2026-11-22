using Minio.Exceptions;

namespace TOEICMaster.Infrastructure.Storage;

internal static class MinioStorageGuard
{
    public static async Task<T> ExecuteDownloadAsync<T>(
        Func<Task<T>> action,
        Func<Exception, Exception>? notFoundFactory = null)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (exception is ObjectNotFoundException or BucketNotFoundException)
        {
            throw notFoundFactory?.Invoke(exception)
                ?? new FileNotFoundException("Stored media object was not found.", exception);
        }
    }
}
