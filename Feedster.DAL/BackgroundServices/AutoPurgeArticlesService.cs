using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Feedster.DAL.BackgroundServices;

/// <summary>
/// This service automatically purges articles and images of said articles after specific user defined setting
/// </summary>
public class AutoPurgeArticlesService : BackgroundService
{
    private  ArticleRepository _articleRepo;
    private  UserRepository _userRepository;
    private IServiceScopeFactory _scopeFactory;

    public AutoPurgeArticlesService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
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
                await DoWork(_userSettings.ArticleRefreshAfterMinutes, _userSettings.ArticleExpirationAfterDays);
            } 
        }
    }
    private async Task DoWork(int interval, int ExpirationAfterDays)
    {
        await _articleRepo.ClearArticlesOlderThan(DateTime.Now.AddDays(-ExpirationAfterDays));
        await Task.Delay(interval * 60 * 1000);
    }
}