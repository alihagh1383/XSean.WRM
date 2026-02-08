using WRM.Core.Interface;
using WRM.Core.Plugins.Http.Classes;

namespace WRM.Core.Plugins.Firewall.Steps;

public sealed class FirewallStep : IPipelineStep
{
    private readonly FirewallEngine _engine;

    public FirewallStep(FirewallEngine engine)
    {
        _engine = engine;
    }

    public Task InvokeAsync(
        NetworkContext ctx,
        Func<NetworkContext, Task> next)
    {
        if (!ctx.Items.TryGetValue("http", out var h))
        {
            Console.WriteLine("🧱 Firewall: No HTTP context → Allow");
            return next(ctx);
        }

        var http = (HttpContext)h;

        Console.WriteLine(
            $"🧱 Firewall: {http.Request.Method} {http.Request.Path}");

        var decision = _engine.Evaluate(ctx);

        if (decision == FirewallDecision.Block)
        {
            Console.WriteLine("🔥 Firewall: Blocked by rule");
            ctx.Connection.Dispose();
            return Task.CompletedTask;
        }

        Console.WriteLine("🧱 Firewall: Allowed");
        return next(ctx);
        // var decision = _engine.Evaluate(ctx);
        //
        // if (decision == FirewallDecision.Block)
        // {
        //     Console.WriteLine("🔥 Firewall: Blocked");
        //     ctx.Connection.Dispose();
        //     return Task.CompletedTask;
        // }
        //
        // return next();
    }
}