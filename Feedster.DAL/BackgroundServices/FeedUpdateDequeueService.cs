using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

/// <summary>
/// This service automatically runs every second and tries to dequeue tasks (fetching feeds) from the BackgroundJobs List
/// </summary>
public class FeedUpdateDequeueService(
    IServiceScopeFactory scopeFactory,
    ILogger<FeedUpdateDequeueService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<FeedUpdateDequeueService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started FeedUpdateDequeueService");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var backgroundJobs = scope.ServiceProvider.GetRequiredService<BackgroundJobs>();
        var rssFetchService = scope.ServiceProvider.GetRequiredService<RssFetchService>();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (backgroundJobs.BackgroundTasks.TryDequeue(out var feeds))
            {
                await rssFetchService.RefreshFeeds(feeds);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        _logger.LogInformation("Stopped FeedUpdateDequeueService");
    }
}