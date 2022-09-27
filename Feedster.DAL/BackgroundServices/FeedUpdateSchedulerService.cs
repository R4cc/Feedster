using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

public class FeedUpdateSchedulerService : BackgroundService
{
    private  BackgroundJobs _backgroundJobs;
    private  UserRepository _userRepository;
    private  FeedRepository _feedRepository;
    private IServiceScopeFactory _scopeFactory;
    private ILogger<FeedUpdateSchedulerService> _logger;

    public FeedUpdateSchedulerService(IServiceScopeFactory scopeFactory, ILogger<FeedUpdateSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started FeedUpdateSchedulerService");
        
        using var scope = _scopeFactory.CreateScope();
        _backgroundJobs = scope.ServiceProvider.GetRequiredService<BackgroundJobs>();   
        
        while (!stoppingToken.IsCancellationRequested)
        {
            _feedRepository = scope.ServiceProvider.GetRequiredService<FeedRepository>();   
            
            // load user settings
            _userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();
            UserSettings _userSettings = await _userRepository.Get();

            if (_userSettings.ArticleRefreshAfterMinutes == 0)
            {
                // auto fetch turned off; recheck in 15 minutes
                await Task.Delay(15 * 60 * 1000, stoppingToken);
            }
            else
            {
                List<Feed> feeds = await _feedRepository.GetAll();
                _backgroundJobs.BackgroundTasks.Enqueue(feeds);
                await Task.Delay(_userSettings.ArticleRefreshAfterMinutes * 60 * 1000, stoppingToken);
            }
        }
        scope.Dispose();
        _logger.LogInformation("Stopped FeedUpdateSchedulerService");
    }
}