using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using ContractAI.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace ContractAI.Services.Storage;

// Persists PDFs to any S3-compatible store; configured against MinIO in local dev.
// The bucket is created on first write rather than at startup so the service does
// not fail to construct when the object store is briefly unreachable.
public sealed class S3BlobStorageService(IAmazonS3 client, IOptions<StorageOptions> options)
    : IBlobStorageService
{
    private readonly StorageOptions _options = options.Value;

    // Guards the create-bucket check so concurrent uploads race it once at most;
    // reset to run again only if the check throws.
    private Task? _ensureBucket;
    private readonly object _ensureBucketGate = new();

    public async Task<string> UploadAsync(
        Stream content,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                InputStream = content,
                ContentType = contentType,
                // The SDK streams the body; without an explicit length it would
                // buffer the whole PDF to compute one, which defeats streaming a
                // 50 MB upload.
                AutoCloseStream = false,
            },
            cancellationToken);

        return $"s3://{_options.BucketName}/{objectKey}";
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var response = await client.GetObjectAsync(_options.BucketName, objectKey, cancellationToken);

        // Copied out of the response so the caller holds a plain seekable stream and
        // the HTTP response (and its socket) is released now rather than living for
        // as long as the caller keeps reading.
        var buffer = new MemoryStream();
        using (response)
        {
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
        }

        buffer.Position = 0;
        return buffer;
    }

    private Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        lock (_ensureBucketGate)
        {
            // A faulted or null task means the last attempt failed or none ran, so
            // start a fresh one; a completed task means the bucket is known present.
            if (_ensureBucket is null || _ensureBucket.IsFaulted)
            {
                _ensureBucket = CreateBucketIfMissingAsync(cancellationToken);
            }

            return _ensureBucket;
        }
    }

    private async Task CreateBucketIfMissingAsync(CancellationToken cancellationToken)
    {
        if (await AmazonS3Util.DoesS3BucketExistV2Async(client, _options.BucketName))
        {
            return;
        }

        try
        {
            await client.PutBucketAsync(
                new PutBucketRequest { BucketName = _options.BucketName },
                cancellationToken);
        }
        catch (AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // Another node created it between the existence check and here; the
            // post-condition (bucket exists) already holds.
        }
    }
}
