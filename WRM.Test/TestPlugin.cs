using System.Text;
using WRM.HTTP;
using WRM.Interface;

namespace WRM.Test;

public class TestPlugin : IPlugin
{
    public string Name => "Stream Test";

    public void Register(IPluginHost host)
    {
        host.Use<TestStep>();
    }
}

public class TestStep : IPipelineStep
{
    public async Task InvokeAsync(NetworkContext ctx, Func<NetworkContext, Task> next)
    {
        if (!ctx.Items.TryGetValue("HTTP_PROTOCOL", out var p)) ;
        if (!ctx.Items.TryGetValue("HTTP_CONTEXT", out var c) || c is not HttpContext context)
        {
            await next(ctx);
            return;
        }

        ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info, $"{p} {context?.Request.Version} {context?.Request.Method} {context?.Request.Path}");
        var req = context!.Request;

        var body = "";
        body += ($"{req.Method} {req.Path}\r\n");
        body = req.Headers.Aggregate(body, (current, header) => current + ($"{header.Key}: {header.Value}\r\n"));
        context?.Response = new HttpResponse { Body = new MemoryStream(Encoding.ASCII.GetBytes(body)) };
        await next(ctx);
    }
}