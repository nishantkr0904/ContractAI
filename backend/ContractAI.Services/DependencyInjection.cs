using Amazon.Runtime;
using Amazon.S3;
using ContractAI.Core.Interfaces;
using ContractAI.Services.Analysis;
using ContractAI.Services.Parsing;
using ContractAI.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContractAI.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddContractAiServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // NativeClauseParser and PdfPigTextExtractor hold no state and are
        // re-entrant, so a single instance of each serves every request.
        services.AddSingleton<IClauseParser, NativeClauseParser>();
        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();

        // The queue is process-wide; the analysis service is scoped because it
        // depends on the scoped DbContext, and the worker resolves it per item
        // inside its own scope.
        services.AddSingleton<IContractProcessingQueue, ContractProcessingQueue>();
        services.AddScoped<IContractAnalysisService, ContractAnalysisService>();
        services.AddHostedService<ContractProcessingWorker>();

        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ForcePathStyle is mandatory for MinIO: it serves buckets as a path
        // segment (host/bucket/key), not as a virtual-host subdomain the way real
        // S3 does, so the default addressing would resolve the wrong host.
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = options.ServiceUrl,
                    ForcePathStyle = true,
                });
        });

        services.AddSingleton<IBlobStorageService, S3BlobStorageService>();

        return services;
    }
}
