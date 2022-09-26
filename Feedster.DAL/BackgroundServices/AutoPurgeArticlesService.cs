using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Feedster.DAL.BackgroundServices;

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
        using var scope = _scopeFactory.CreateScope();
        _articleRepo = scope.ServiceProvider.GetRequiredService<ArticleRepository>();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            _userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();
            UserSettings _userSettings = await _userRepository.Get();

            if (_userSettings.ArticleExpirationAfterDays == 0)
            {
                await Task.Delay(15 * 60 * 1000);
            }
            else
            {
                await DoWork(_userSettings.ArticleRefreshAfterMinutes, _userSettings.ArticleExpirationAfterDays);
            }
        }
        scope.Dispose();
    }
    private async Task DoWork(int interval, int ExpirationAfterDays)
    {
        await _articleRepo.ClearArticlesOlderThan(DateTime.Now.AddDays(-ExpirationAfterDays));
        await Task.Delay(interval * 60 * 1000);
    }
}