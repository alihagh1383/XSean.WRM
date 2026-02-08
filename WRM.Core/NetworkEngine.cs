using System;
using WRM.Interface;

namespace WRM.Core;

public sealed class NetworkEngine : INetworkEngine
{
    private readonly PipelineBuilder _pipeline;

    private readonly Func<NetworkContext, Task> _cachedApp;

    private readonly ILoger _loger;

    public NetworkEngine(PipelineBuilder pipeline, ILoger loger)
    {
        _pipeline = pipeline;
        _loger = loger;
        _cachedApp = pipeline.Build();
    }

    public async Task HandleAsync(IConnection connection, CancellationToken ct)
    {
        var ctx = new NetworkContext(connection)
        {
            Loger = _loger,
            Cancellation = ct
        };

        //var app = _pipeline.Build();
        //await app(ctx);
        await _cachedApp(ctx);
    }
}