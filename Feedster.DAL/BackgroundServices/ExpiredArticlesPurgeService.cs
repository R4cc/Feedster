using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feedster.DAL.BackgroundServices;

/// <summary>
/// This service automatically purges articles and images of said articles after specific user defined setting
/// </summary>
public class ExpiredArticlesPurgeService : BackgroundService
{
    private ArticleRepository? _articleRepo;
    private UserRepository? _userRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredArticlesPurgeService> _logger;

    public ExpiredArticlesPurgeService(IServiceScopeFactory scopeFactory, ILogger<ExpiredArticlesPurgeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started ExpiredArticlesPurgeService. Waiting 10 minutes until first purge");

        // wait 10 minutes before starting service to let the updater update first
        await Task.Delay(10 * 60 * 1000, stoppingToken);

        _logger.LogInformation("Starting first purge");

        while (!stoppingToken.IsCancellationRequested)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            _articleRepo = scope.ServiceProvider.GetRequiredService<ArticleRepository>();

            // Load user settings
            _userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();
            UserSettings _userSettings = await _userRepository.Get();

            if (_userSettings.ArticleExpirationAfterDays == 0)
            {
                // auto purge turned off; recheck in 15 minutes
                await Task.Delay(15 * 60 * 1000, stoppingToken);
            }
            else
            {
                await _articleRepo.ClearArticlesOlderThan(DateTime.Now.AddDays(-_userSettings.ArticleExpirationAfterDays));

                // run once every 24 hours
                _logger.LogInformation("Purge completed. Waiting 24 Hours until next purge");

                scope.Dispose();
                _userRepository.Dispose();

                await Task.Delay(24 * 60 * 60 * 1000, stoppingToken);
            }
        }
    }
}