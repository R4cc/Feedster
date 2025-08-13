using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

/// <summary>
/// This service automatically updates all feeds in the DB on a user defined interval
/// </summary>
public class FeedUpdateSchedulerService : BackgroundService
{
    private  BackgroundJobs? _backgroundJobs;
    private  UserRepository? _userRepository;
    private  FeedRepository? _feedRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeedUpdateSchedulerService> _logger;

    public FeedUpdateSchedulerService(IServiceScopeFactory scopeFactory, ILogger<FeedUpdateSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started FeedUpdateSchedulerService");
        
        using IServiceScope scope = _scopeFactory.CreateScope();
        _backgroundJobs = scope.ServiceProvider.GetRequiredService<BackgroundJobs>();   
        
        while (!stoppingToken.IsCancellationRequested)
        {
            _feedRepository = scope.ServiceProvider.GetRequiredService<FeedRepository>();   
            
            // load user settings
            _userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();
            UserSettings userSettings = await _userRepository.Get();

            if (userSettings.ArticleRefreshAfterMinutes == 0)
            {
                // auto fetch turned off; recheck in 15 minutes
                await Task.Delay(15 * 60 * 1000, stoppingToken);
            }
            else
            {
                // Update all Feeds currently in DB
                List<Feed> feeds = await _feedRepository.GetAll();
                _backgroundJobs.BackgroundTasks.Enqueue(feeds);
                await Task.Delay(userSettings.ArticleRefreshAfterMinutes * 60 * 1000, stoppingToken);
            }
        }
        _logger.LogInformation("Stopped FeedUpdateSchedulerService");
    }
}