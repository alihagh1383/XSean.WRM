using System.Net.Sockets;
using WRM.Core.Interfaces;

namespace WRM.Core.Plugins.Tcp.Steps;

public class DetactionTcpStep : IPluginStep
{
    public async Task InvokeAsync(WRMContext context, Func<WRMContext, Task> next)
    {
        if (!context.Items.TryGetValue(Names.Socket, out var value) || value is not Socket { SocketType: SocketType.Stream } socket)
        {
            await next(context);
            return;
        }


        context.Loger?.LogInfo(this, $"Socket Is Stream");
        var sream = new NetworkStream(socket, false);
        
        context.Items[Names.RowTcpStream] = context.Items[Names.TcpStream] = sream;
        
        await next(context);

        await sream.DisposeAsync();
        socket.Close();
        context.Loger?.LogInfo(this, $"Done");
    }
}