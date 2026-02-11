using WRM.Core.Interfaces;
using WRM.Core.Streams;

namespace WRM.Core.Plugins.TcpToSSL.Steps;

public class SSLDetectionStep : IPluginStep
{
    public async Task InvokeAsync(WRMContext context, Func<WRMContext, Task> next)
    {
        if (!context.Items.TryGetValue(Names.TcpStream, out var value) || value is not Stream { } stream)
        {
            await next(context);
            return;
        }
        {
            byte[] buffer;
            int read;

            buffer = new byte[3];
            read = await stream.ReadAsync(buffer, context.CancellationToken);

            if (read >= 3 &&
                buffer[0] == 0x16 && // Handshake
                buffer[1] == 0x03 && // TLS major
                buffer[2] <= 0x04)
            {
                context.Loger?.LogInfo(this, $"Stream Is SSL : True");
                context.Items[Names.IsSsl] = true;
            }
            else
            {
                context.Loger?.LogInfo(this, $"Stream Is SSL : False");
                context.Items[Names.IsSsl] = false;
            }

            context.Items[Names.TcpStream] = new BufferedPeekStream(stream, buffer);
        }
        await next(context);
    }
}