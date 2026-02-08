using WRM.Core.Interface;
using WRM.Core.Plugins.Firewall.Rules;
using WRM.Core.Plugins.Firewall.Steps;

namespace WRM.Core.Plugins.Firewall;

public sealed class FirewallPlugin(params IFirewallRule[] rules) : IPlugin
{
    public string Name => "Firewall";

    public void Register(IPluginHost host)
    {
        var engine = new FirewallEngine(rules);
        host.AddService(engine);
        host.Use<FirewallStep>();
    }
}
