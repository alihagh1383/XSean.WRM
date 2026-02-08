using WRM.Core.Interface;
using WRM.Core.Plugins.Http.Classes;
using WRM.Core.Plugins.ProtocolDetection;
using WRM.Core.Streams.Tools;

namespace WRM.Core.Plugins.Http.Steps;

public sealed class Http1ParsingStep : IPipelineStep
{
    public async Task InvokeAsync(
        NetworkContext ctx,
        Func<NetworkContext,Task> next)
    {
        if (!ctx.Items.TryGetValue("protocol", out var p) ||
            (DetectedProtocol)p != DetectedProtocol.Http1)
        {
            await next(ctx);
            return;
        }

        var reader = new LineReader(ctx.Connection.Stream);

        var requestLine = await reader.ReadLineAsync(ctx.Cancellation);
        if (string.IsNullOrWhiteSpace(requestLine))
            return;

        var parts = requestLine.Split(' ', 3);
        if (parts.Length < 3)
            return;

        var req = new HttpRequest()
        {
            Method = parts[0],
            Path = parts[1],
            Version = parts[2]
        };

        while (true)
        {
            var line = await reader.ReadLineAsync(ctx.Cancellation);
            if (string.IsNullOrEmpty(line))
                break;

            int idx = line.IndexOf(':');
            if (idx <= 0) continue;

            var name = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();

            req.Headers[name] = value;
        }

        ctx.Items["http"] = new HttpContext
        {
            Request = req
        };

        await next(ctx);
    }
}
