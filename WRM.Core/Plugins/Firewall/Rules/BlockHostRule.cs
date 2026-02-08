using WRM.Core.Plugins.Http.Classes;

namespace WRM.Core.Plugins.Firewall.Rules;

public sealed class BlockHostRule : IFirewallRule
{
    private readonly HashSet<string> _blocked;

    public BlockHostRule(IEnumerable<string> hosts)
    {
        _blocked = hosts
            .Select(h => h.ToLowerInvariant())
            .ToHashSet();
    }

    public FirewallDecision Evaluate(NetworkContext ctx)
    {
        if (!ctx.Items.TryGetValue("http", out var h))
            return FirewallDecision.Allow;

        var http = (HttpContext)h;
        if (http.Request.IsConnect)
        {
            var host = http.Request.Path
                .Split(':', 2)[0]
                .ToLowerInvariant();

            return _blocked.Contains(host)
                ? FirewallDecision.Block
                : FirewallDecision.Allow;
        }
        if (http.Request.Headers.TryGetValue("Host", out var hdr))
        {
            var host = hdr.Split(':')[0].ToLowerInvariant();
            if (_blocked.Contains(host))
                return FirewallDecision.Block;
        }


        return FirewallDecision.Allow;
    }
}
