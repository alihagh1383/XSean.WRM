using WRM.Interface;
using WRM.WrappedConnection.Streams;

namespace WRM.SSL.Steps;

public sealed class SSLDetectionStep() : IPipelineStep
{
    private const int PeekSize = 32;

    public async Task InvokeAsync(NetworkContext ctx, Func<NetworkContext, Task> next)
    {
        var stream = ctx.Connection.Stream;
        var buffer = new byte[PeekSize];
        int read = await stream.ReadAsync(
            buffer, 0, buffer.Length, ctx.Cancellation);

        if (read >= 3 &&
            buffer[0] == 0x16 && // Handshake
            buffer[1] == 0x03 && // TLS major
            buffer[2] <= 0x04)
            ctx.Items["IsSSl"] = true;
        ctx.Connection = new WrappedConnection.Connections.WrappedConnection(
            ctx.Connection,
            new BufferedPeekStream(stream, buffer[..read])
        );
        await next(ctx);
    }
}