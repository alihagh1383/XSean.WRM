using System.Net.Security;
using WRM.Interface;

namespace WRM.SSL.Steps;

public class MoveToSSLStep(SslServerAuthenticationOptions options) : IPipelineStep
{
    public async Task InvokeAsync(NetworkContext ctx, Func<NetworkContext, Task> next)
    {
        if (!ctx.Items.TryGetValue("IsSSl", out var p) || !(bool)p)
        {
            await next(ctx);
            return;
        }
        SslStream? sslStream = null;
        sslStream = new SslStream(ctx.Connection.Stream, false);
        await sslStream.AuthenticateAsServerAsync(options, ctx.Cancellation);
        ctx.Connection = new WrappedConnection.Connections.WrappedConnection(ctx.Connection, sslStream);
        await next(ctx);
    }
}