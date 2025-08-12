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

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var feedRepository = scope.ServiceProvider.GetRequiredService<IFeedRepository>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var backgroundJobs = scope.ServiceProvider.GetRequiredService<BackgroundJobs>();

            UserSettings userSettings = await userRepository.Get(stoppingToken);

            if (userSettings.ArticleRefreshAfterMinutes == 0)
            {
                // auto fetch turned off; recheck in 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            else
            {
                // Update all Feeds currently in DB
                List<Feed> feeds = await feedRepository.GetAll(stoppingToken);
                backgroundJobs.BackgroundTasks.Enqueue(feeds);
                await Task.Delay(TimeSpan.FromMinutes(userSettings.ArticleRefreshAfterMinutes), stoppingToken);
            }
        }

        _logger.LogInformation("Stopped FeedUpdateSchedulerService");
    }
}
