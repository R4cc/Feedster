using Feedster.DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Feedster.DAL.BackgroundServices;

public class AutoFetchService : BackgroundService
{
    private  RssFetchService _rssFetchService;
    private IServiceScopeFactory _scopeFactory;

    public AutoFetchService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        _rssFetchService = scope.ServiceProvider.GetRequiredService<RssFetchService>();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWork();
        }
        scope.Dispose();
    }
    private async Task DoWork()
    {
        await _rssFetchService.RefreshFeeds();
        await Task.Delay(10 * 60 * 1000);
    }
}