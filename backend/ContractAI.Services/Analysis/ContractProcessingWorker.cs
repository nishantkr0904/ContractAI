using ContractAI.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContractAI.Services.Analysis;

// Drains the processing queue and runs analysis for each contract. Each item gets
// its own DI scope so the scoped DbContext is not shared across contracts, and a
// failure on one contract is logged and swallowed so the loop keeps serving the
// rest — the contract's own status is moved to PARSED_ERROR by the analysis
// service, which is where a failure becomes visible to the client.
public sealed class ContractProcessingWorker(
    IContractProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ContractProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid contractId;
            try
            {
                contractId = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var analysis = scope.ServiceProvider.GetRequiredService<IContractAnalysisService>();

            try
            {
                await analysis.ProcessContractAsync(contractId, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Background analysis failed for contract {ContractId}", contractId);
            }
        }
    }
}
