using WRM.Core.Plugins.Http.Classes;

namespace WRM.Core.Plugins.Firewall.Rules;

public sealed class BlockMethodRule : IFirewallRule
{
    private readonly string _method;

    public BlockMethodRule(string method)
    {
        _method = method;
    }

    public FirewallDecision Evaluate(NetworkContext ctx)
    {
        if (!ctx.Items.TryGetValue("http", out var h))
            return FirewallDecision.Allow;

        var http = (HttpContext)h;
        return http.Request.Method.Equals(
            _method, StringComparison.OrdinalIgnoreCase)
            ? FirewallDecision.Block
            : FirewallDecision.Allow;
    }
}
