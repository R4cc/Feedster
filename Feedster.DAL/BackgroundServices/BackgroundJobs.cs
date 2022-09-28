using System.Collections.Concurrent;
using Feedster.DAL.Models;

namespace Feedster.DAL.BackgroundServices;

public class BackgroundJobs
{
    public ConcurrentQueue<List<Feed>> BackgroundTasks { get; set; } = new();
}