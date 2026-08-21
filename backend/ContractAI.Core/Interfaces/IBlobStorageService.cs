namespace ContractAI.Core.Interfaces;

// Abstracts the object store the contract PDFs live in. The interface stays in
// Core so the orchestration service depends on the capability, not on the S3 SDK;
// ContractAI.Services binds it to MinIO/S3.
public interface IBlobStorageService
{
    // Stores content under objectKey and returns the canonical blob URI
    // (s3://bucket/key) recorded on the contract row. The caller owns the stream.
    Task<string> UploadAsync(
        Stream content,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default);

    // Opens the stored object for reading. The caller owns and must dispose the
    // returned stream. objectKey is the key, not the s3:// URI.
    Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);
}

// contracts.file_uri stores the full s3://bucket/key form, but the storage API
// addresses objects by key, so the two are converted here rather than in each
// caller. Parsed by hand because System.Uri's handling of a non-registered scheme
// is not something worth depending on.
public static class BlobUri
{
    private const string Scheme = "s3://";

    public static string ToObjectKey(string blobUri)
    {
        if (!blobUri.StartsWith(Scheme, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{blobUri}' is not an s3:// URI.", nameof(blobUri));
        }

        var separator = blobUri.IndexOf('/', Scheme.Length);
        if (separator < 0 || separator == blobUri.Length - 1)
        {
            throw new ArgumentException($"'{blobUri}' has no object key.", nameof(blobUri));
        }

        return blobUri[(separator + 1)..];
    }
}
