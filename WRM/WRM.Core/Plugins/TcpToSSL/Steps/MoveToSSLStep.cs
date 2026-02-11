using System.Net.Security;
using WRM.Core.Interfaces;

namespace WRM.Core.Plugins.TcpToSSL.Steps;

public class MoveToSSLStep(SslServerAuthenticationOptions options) : IPluginStep
{
    public async Task InvokeAsync(WRMContext context, Func<WRMContext, Task> next)
    {
        if (!context.Items.TryGetValue(Names.IsSsl, out var p)
            || !(bool)p
            || !context.Items.TryGetValue(Names.TcpStream, out var value)
            || value is not Stream stream)
        {
            await next(context);
            return;
        }

        var sslStream = new SslStream(stream, false);
        await sslStream.AuthenticateAsServerAsync(options, context.CancellationToken);
        
        context.Items[Names.TcpStream] = sslStream;
        
        await next(context);

        await sslStream.DisposeAsync();
    }
}