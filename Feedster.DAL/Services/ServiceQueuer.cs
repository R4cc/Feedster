using Feedster.DAL.BackgroundServices;
using Feedster.DAL.Models;

namespace Feedster.DAL.Services;

public class ServiceQueuer
{
    private readonly BackgroundJobs _backgroundJobs;
    public ServiceQueuer(BackgroundJobs backgroundJobs)
    {
        _backgroundJobs = backgroundJobs;
    }

    public async Task FireAndForgetEndPoint(List<Feed> feeds){
        _backgroundJobs.BackgroundTasks.Enqueue(feeds);
    }
}