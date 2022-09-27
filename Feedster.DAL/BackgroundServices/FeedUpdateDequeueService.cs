using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

public class FeedUpdateDequeueService : BackgroundService
{
    private  RssFetchService _rssFetchService;
    private  BackgroundJobs _backgroundJobs;
    private  UserRepository _userRepository;
    private IServiceScopeFactory _scopeFactory;
    private ILogger<FeedUpdateDequeueService> _logger;

    public FeedUpdateDequeueService(IServiceScopeFactory scopeFactory, ILogger<FeedUpdateDequeueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started FeedUpdateDequeueService");
        
        using var scope = _scopeFactory.CreateScope();
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
        scope.Dispose();
        _logger.LogInformation("Stopped FeedUpdateDequeueService");
    }
}