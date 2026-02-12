using WRM.Core.Interfaces;

namespace WRM.Core.Plugins.Http.Abstraction;

public abstract class HttpContext : IDisposable, IAsyncDisposable
{
    public required Stream Connection;
    public required HttpRequest Request;
    public required CancellationToken CancellationToken;
    public ILoger? Loger;
    protected bool ResponseWrited = false;
    public abstract Task WriteResponse(HttpResponse response, Stream body, CancellationToken ct = default);
    public abstract Task MakeTunel(HttpResponse response, out Stream read, out Stream write, CancellationToken ct = default);


    public void Dispose() => GC.SuppressFinalize(this);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    ~HttpContext()
    {
        if (ResponseWrited) return;
        try
        {
            WriteResponse(new HttpResponse { StatusCode = 500 }, Stream.Null).GetAwaiter().GetResult();
        }
        catch
        {
            // ignored
        }
    }
}