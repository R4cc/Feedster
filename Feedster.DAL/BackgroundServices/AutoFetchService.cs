using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Feedster.DAL.BackgroundServices;

public class AutoFetchService : BackgroundService
{
    private  RssFetchService _rssFetchService;
    private  UserRepository _userRepository;
    private IServiceScopeFactory _scopeFactory;

    public AutoFetchService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            _rssFetchService = scope.ServiceProvider.GetRequiredService<RssFetchService>();   
            
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
                await DoWork(_userSettings.ArticleRefreshAfterMinutes);
            }
        }
    }
    private async Task DoWork(int interval)
    {
        await _rssFetchService.RefreshFeeds();
        await Task.Delay(interval * 60 * 1000);
    }
}