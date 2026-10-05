using Feedster.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

public class FeedUpdateSchedulerService(
    IServiceScopeFactory scopeFactory,
    BackgroundJobs backgroundJobs,
    ILogger<FeedUpdateSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Started FeedUpdateSchedulerService");
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            using (var scope = scopeFactory.CreateScope())
            {
                var settings = await scope.ServiceProvider.GetRequiredService<UserRepository>().GetSnapshot(stoppingToken);
                delay = settings.ArticleRefreshAfterMinutes == 0
                    ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(settings.ArticleRefreshAfterMinutes);
                if (settings.ArticleRefreshAfterMinutes != 0)
                {
                    var ids = await scope.ServiceProvider.GetRequiredService<FeedRepository>().GetIds(stoppingToken);
                    backgroundJobs.Enqueue(ids);
                }
            }
            await Task.Delay(delay, stoppingToken);
        }
    }
}
