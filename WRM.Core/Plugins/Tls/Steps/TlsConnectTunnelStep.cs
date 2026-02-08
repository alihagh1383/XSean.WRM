using System.Net.Sockets;
using System.Text;
using WRM.Core.Interface;
using WRM.Core.Plugins.Http.Classes;

namespace WRM.Core.Plugins.Tls.Steps;

public sealed class TlsConnectTunnelStep : IPipelineStep
{
    public async Task InvokeAsync(
        NetworkContext ctx,
        Func<NetworkContext,Task> next)
    {
        if (!ctx.Items.TryGetValue("http", out var h))
        {
            await next(ctx);
            return;
        }

        var http = (HttpContext)h;
        if (!http.Request.IsConnect)
        {
            await next(ctx);
            return;
        }

        var target = http.Request.Path; // host:port
        var parts = target.Split(':', 2);
        if (parts.Length != 2)
            return;

        var host = parts[0];
        var port = int.Parse(parts[1]);

        // اتصال به مقصد
        var remote = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp);

        await remote.ConnectAsync(host, port);

        // پاسخ CONNECT OK
        var response =
            "HTTP/1.1 200 Connection Established\r\n" +
            "Proxy-Agent: CoreProxy\r\n\r\n";

        var bytes = Encoding.ASCII.GetBytes(response);
        await ctx.Connection.Stream.WriteAsync(
            bytes, 0, bytes.Length, ctx.Cancellation);

        Console.WriteLine($"🔐 TLS Tunnel established to {host}:{port}");

        // شروع Tunnel
        await TunnelAsync(
            ctx.Connection.Stream,
            new NetworkStream(remote, ownsSocket: true),
            ctx.Cancellation
        );
    }

    private static async Task TunnelAsync(
        Stream client,
        Stream server,
        CancellationToken ct)
    {
        var t1 = PipeAsync(client, server, ct);
        var t2 = PipeAsync(server, client, ct);

        await Task.WhenAny(t1, t2);

        try { client.Dispose(); } catch { }
        try { server.Dispose(); } catch { }
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
                read = await src.ReadAsync(buffer, 0, buffer.Length, ct);
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
