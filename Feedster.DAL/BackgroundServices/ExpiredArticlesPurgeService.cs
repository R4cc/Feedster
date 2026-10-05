using Feedster.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

public class ExpiredArticlesPurgeService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredArticlesPurgeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Started ExpiredArticlesPurgeService. Waiting 10 minutes until first purge");
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            using (var scope = scopeFactory.CreateScope())
            {
                var settings = await scope.ServiceProvider.GetRequiredService<UserRepository>().GetSnapshot(stoppingToken);
                delay = settings.ArticleExpirationAfterDays == 0 ? TimeSpan.FromMinutes(15) : TimeSpan.FromDays(1);
                if (settings.ArticleExpirationAfterDays != 0)
                {
                    await scope.ServiceProvider.GetRequiredService<ArticleRepository>()
                        .ClearArticlesOlderThan(DateTime.Now.AddDays(-settings.ArticleExpirationAfterDays));
                    logger.LogInformation("Purge completed. Waiting 24 hours until next purge");
                }
            }
            await Task.Delay(delay, stoppingToken);
        }
    }
}
