using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Globot.Web.App.Services;

public class GlobRequestService
{
    private readonly ConcurrentQueue<PushGlobRequestContext> _items = new ConcurrentQueue<PushGlobRequestContext>();
    private readonly Channel<PushGlobRequestContext> _queue;

    public GlobRequestService()
    {
        _queue = Channel.CreateUnbounded<PushGlobRequestContext>();
    }

    public async Task<PushGlobRequestContext> AddGlobRequest(PushGlobRequest request)
    {
        var item = new PushGlobRequestContext(request);
        _items.Enqueue(item);
        await _queue.Writer.WriteAsync(item);

        return item;
    }

    public IEnumerable<PushGlobRequestContext> GetAll()
    {
        return _items;
    }

    public async Task<PushGlobRequestContext> GetNext(CancellationToken cancelToken)
    {
        var item = await _queue.Reader.ReadAsync(cancelToken);

        return item;
    }
}
