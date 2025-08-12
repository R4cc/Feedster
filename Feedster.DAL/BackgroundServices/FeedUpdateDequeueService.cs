using Feedster.DAL.Models;
using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

/// <summary>
/// This service automatically runs every second and tries to dequeue tasks (fetching feeds) from the BackgroundJobs List
/// </summary>
public class FeedUpdateDequeueService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeedUpdateDequeueService> _logger;

    public FeedUpdateDequeueService(IServiceScopeFactory scopeFactory, ILogger<FeedUpdateDequeueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started FeedUpdateDequeueService");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var backgroundJobs = scope.ServiceProvider.GetRequiredService<BackgroundJobs>();
            var rssFetchService = scope.ServiceProvider.GetRequiredService<RssFetchService>();

            if (backgroundJobs.BackgroundTasks.TryDequeue(out var feeds))
            {
                await rssFetchService.RefreshFeeds(feeds, stoppingToken);
            }

            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("Stopped FeedUpdateDequeueService");
    }
}
