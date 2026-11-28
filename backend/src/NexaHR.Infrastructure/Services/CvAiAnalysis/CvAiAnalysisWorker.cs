using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NexaHR.Infrastructure.Services.CvAiAnalysis;

public sealed class CvAiAnalysisWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CvAiAnalysisWorker> _logger;

    public CvAiAnalysisWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CvAiAnalysisWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var nextRecoveryUtc = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_configuration.GetValue<bool>(
                        "Recruitment:Ai:Enabled"))
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken);

                    continue;
                }

                if (DateTime.UtcNow >= nextRecoveryUtc)
                {
                    await using var recoveryScope =
                        _scopeFactory.CreateAsyncScope();

                    var recovery = recoveryScope
                        .ServiceProvider
                        .GetRequiredService<
                            CvAiAnalysisRecoveryService>();

                    var recovered =
                        await recovery.RecoverInterruptedAsync(
                            stoppingToken);

                    if (recovered > 0)
                    {
                        _logger.LogInformation(
                            "Recovered {Count} interrupted AI analyses.",
                            recovered);
                    }

                    nextRecoveryUtc =
                        DateTime.UtcNow.AddMinutes(1);
                }

                bool processed;

                await using (
                    var scope =
                        _scopeFactory.CreateAsyncScope())
                {
                    var processor = scope
                        .ServiceProvider
                        .GetRequiredService<
                            CvAiAnalysisProcessor>();

                    using var processingDeadline =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                stoppingToken);

                    processingDeadline.CancelAfter(
                        TimeSpan.FromMinutes(8));

                    processed =
                        await processor.ProcessNextAsync(
                            processingDeadline.Token);
                }

                if (!processed)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(3),
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Never log CV contents, prompts,
                // provider responses or exception payloads.
                _logger.LogWarning(
                    "AI worker iteration failed. Recovery will be attempted.");

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
