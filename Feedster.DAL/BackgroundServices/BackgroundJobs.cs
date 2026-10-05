using System.Threading.Channels;

namespace Feedster.DAL.BackgroundServices;

public class BackgroundJobs
{
    private readonly Channel<int> _feeds = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
        AllowSynchronousContinuations = false
    });
    private readonly HashSet<int> _pending = [];
    private readonly Lock _gate = new();

    public void Enqueue(IEnumerable<int> feedIds)
    {
        lock (_gate)
        {
            foreach (var feedId in feedIds)
                if (_pending.Add(feedId)) _feeds.Writer.TryWrite(feedId);
        }
    }

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken) =>
        _feeds.Reader.ReadAllAsync(cancellationToken);

    public void Complete(int feedId)
    {
        lock (_gate) _pending.Remove(feedId);
    }
}
