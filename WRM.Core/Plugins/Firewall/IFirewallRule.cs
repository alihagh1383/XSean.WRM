namespace WRM.Core.Plugins.Firewall;

public interface IFirewallRule
{
    FirewallDecision Evaluate(NetworkContext ctx);
}
public enum FirewallDecision
{
    Allow,
    Block
}