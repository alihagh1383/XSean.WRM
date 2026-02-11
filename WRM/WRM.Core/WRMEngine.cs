using System.Net.Sockets;
using WRM.Core.Interfaces;

namespace WRM.Core;

public class WRMEngine(PluginHost pluginHost, ILoger? loger = null)
{
    private readonly Func<WRMContext, Task> _cachedApp = pluginHost.Build();

    public async Task HandleAsync(Socket socket, CancellationToken ct)
    {
        var context = new WRMContext
        {
            Loger = loger,
            CancellationToken = ct,
            Items =
            {
                ["Socket"] = socket
            }
        };
        await _cachedApp(context);
        socket.Close();
        socket.Dispose();
    }
}