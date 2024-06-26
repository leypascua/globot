using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Globot.Web.App.Services;

public class GlobRequestQueue
{
    private readonly ConcurrentQueue<PushGlobRequestContext> _items = new ConcurrentQueue<PushGlobRequestContext>();
    private readonly Channel<PushGlobRequestContext>_queue;

    public GlobRequestQueue()
    {
        _queue = Channel.CreateBounded<PushGlobRequestContext>(Environment.ProcessorCount * 2);
    }

    public async Task<(PushGlobRequestContext, bool)> Add(PushGlobRequest request)
    {
        var item = new PushGlobRequestContext(request);

        CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        bool canWrite = await _queue.Writer.WaitToWriteAsync(cts.Token);
        
        if (canWrite)
        {
            await _queue.Writer.WriteAsync(item);
            _items.Enqueue(item);
        }

        return (item, canWrite);
    }

    public async Task<PushGlobRequestContext> GetNext(CancellationToken cancelToken)
    {
        var item = await _queue.Reader.ReadAsync(cancelToken);
        return item;
    }

    public IEnumerable<PushGlobRequestContext> GetAll()
    {
        return _items;
    }
}
