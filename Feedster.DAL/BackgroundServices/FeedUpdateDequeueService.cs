using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

public class FeedUpdateDequeueService(
    IServiceScopeFactory scopeFactory,
    BackgroundJobs backgroundJobs,
    ILogger<FeedUpdateDequeueService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Started FeedUpdateDequeueService");
        await foreach (var feedId in backgroundJobs.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Bound the EF change tracker and read current metadata for each job.
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<FeedRepository>();
                var feed = await repository.GetMetadata(feedId, stoppingToken);
                if (feed is not null)
                    await scope.ServiceProvider.GetRequiredService<RssFetchService>().RefreshFeed(feed, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to refresh feed {FeedId}", feedId);
            }
            finally
            {
                backgroundJobs.Complete(feedId);
            }
        }
    }
}
