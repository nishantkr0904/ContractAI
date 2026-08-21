using System.ComponentModel.DataAnnotations;

namespace ContractAI.Services.Storage;

// Bound from the "Storage" configuration section. ServiceUrl and BucketName are
// non-secret and live in appsettings.json; the keys are supplied per environment
// (User Secrets locally, the secret store in deployment).
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string ServiceUrl { get; set; } = null!;

    [Required]
    public string BucketName { get; set; } = null!;

    [Required]
    public string AccessKey { get; set; } = null!;

    [Required]
    public string SecretKey { get; set; } = null!;
}
