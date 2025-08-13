using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeProvider = System.TimeProvider;

namespace Feedster.DAL.BackgroundServices;

/// <summary>
/// This service automatically purges articles and images of said articles after specific user defined setting
/// </summary>
public class ExpiredArticlesPurgeService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredArticlesPurgeService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ExpiredArticlesPurgeService> _logger = logger;
    private readonly TimeProvider _timeProvider = timeProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started ExpiredArticlesPurgeService. Waiting 10 minutes until first purge");

        // wait 10 minutes before starting service to let the updater update first
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        _logger.LogInformation("Starting first purge");

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var articleRepo = scope.ServiceProvider.GetRequiredService<ArticleRepository>();
            var userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();
            UserSettings userSettings = await userRepository.Get();

            if (userSettings.ArticleExpirationAfterDays == 0)
            {
                // auto purge turned off; recheck in 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            else
            {
                await articleRepo.ClearArticlesOlderThan(
                    _timeProvider.GetLocalNow().DateTime.AddDays(-userSettings.ArticleExpirationAfterDays));

                // run once every 24 hours
                _logger.LogInformation("Purge completed. Waiting 24 Hours until next purge");

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}