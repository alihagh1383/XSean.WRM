using WRM.Core.Interface;

namespace WRM.Core;

public sealed class NetworkEngine:INetworkEngine
{
    private readonly PipelineBuilder _pipeline;

    public NetworkEngine(PipelineBuilder pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task HandleAsync(IConnection connection, CancellationToken ct)
    {
        var ctx = new NetworkContext
        {
            Connection = connection,
            Cancellation = ct
        };

        var app = _pipeline.Build();
        await app(ctx);
    }
}
