using System.Net.Sockets;
using System.Text;
using WRM.Core;
using WRM.Core.Interfaces;
using WRM.Core.Plugins.Http.Abstraction;
using WRM.Core.Tools;

namespace WRM.Test;

public class TestPlugin : IPlugin
{
    public string Name => "Stream Test";

    public void RegesterSteps(PluginHost pluginHost)
    {
        pluginHost.RegesterStep(() => new TestStep());
    }
}

public class TestStep : IPluginStep
{
    public async Task InvokeAsync(WRMContext context, Func<WRMContext, Task> next)
    {
        if (context.Items.TryGetValue(Names.HttpContext, out var objectHttpContext) && objectHttpContext is HttpContext httpContext)
        {
            if (httpContext.Request.Method.StartsWith("Connect", StringComparison.OrdinalIgnoreCase))
            {
                var target = httpContext.Request.Path; // host:port
                var parts = target.Split(':', 2);
                if (parts.Length != 2) return;
                var host = parts[0];
                var port = int.Parse(parts[1]);
                var remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await remote.ConnectAsync(host, port);
                await httpContext.MakeTunel(new HttpResponse() { ReasonPhrase = "Connection Established" }, out var read, out var write);
                var server = new NetworkStream(remote, ownsSocket: true);
                var t1 = PipeAsync(read, server, context.CancellationToken);
                var t2 = PipeAsync(server, write, context.CancellationToken);
                context.Loger?.LogInfo(this, httpContext.Request.Method);
                await Task.WhenAny(t1, t2);
            }
            else
            {
                context.Loger?.LogInfo(this, "Find Http Context ");
                await httpContext.WriteResponse(new HttpResponse() { StatusCode = 200, ReasonPhrase = "Ok" }, new MemoryStream(Encoding.ASCII.GetBytes(
                    $"""
                     {httpContext.Request.IsSsl}
                     {httpContext.Request.Method} {httpContext.Request.Path} {httpContext.Request.Version}
                     {string.Join("\n", httpContext.Request.Headers)}   
                     """)));
            }
        }

        await next(context);
    }

    private static async Task PipeAsync(
        Stream src,
        Stream dst,
        CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];

        while (!ct.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await src.ReadAsync(buffer, ct);
            }
            catch
            {
                break;
            }

            if (read <= 0)
                break;

            await dst.WriteAsync(buffer, 0, read, ct);
            await dst.FlushAsync(ct);
        }
    }
}