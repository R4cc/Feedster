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
public class FeedUpdateDequeueService : BackgroundService
{
    private  RssFetchService? _rssFetchService;
    private  BackgroundJobs? _backgroundJobs;
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
        
        using IServiceScope scope = _scopeFactory.CreateScope();
        _backgroundJobs = scope.ServiceProvider.GetRequiredService<BackgroundJobs>();   
        _rssFetchService = scope.ServiceProvider.GetRequiredService<RssFetchService>();   
        
        while (!stoppingToken.IsCancellationRequested)
        {
            if(_backgroundJobs.BackgroundTasks.TryDequeue(out var feeds))
            {
                await _rssFetchService.RefreshFeeds(feeds); 
            }
            await Task.Delay(1000, stoppingToken);
        }
        _logger.LogInformation("Stopped FeedUpdateDequeueService");
    }
}