namespace WRM.Core.Plugins.Firewall;

public sealed class FirewallEngine
{
    private readonly List<IFirewallRule> _rules;

    public FirewallEngine(IEnumerable<IFirewallRule> rules)
    {
        _rules = rules.ToList();
    }

    public FirewallDecision Evaluate(NetworkContext ctx)
    {
        foreach (var rule in _rules)
        {
            var decision = rule.Evaluate(ctx);
            if (decision == FirewallDecision.Block)
                return FirewallDecision.Block;
        }

        return FirewallDecision.Allow;
    }
}
