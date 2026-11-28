using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NexaHR.Infrastructure.Services.CvExtractions;

public sealed class CvExtractionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CvExtractionWorker> _logger;

    public CvExtractionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CvExtractionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var processor = scope.ServiceProvider
                    .GetRequiredService<CvExtractionProcessor>();

                var processed = await processor.ProcessNextAsync(
                    stoppingToken);

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
                _logger.LogWarning(
                    "CV extraction worker operation failed.");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }
}
