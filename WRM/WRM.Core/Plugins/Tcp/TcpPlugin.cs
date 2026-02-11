using WRM.Core.Interfaces;
using WRM.Core.Plugins.Tcp.Steps;

namespace WRM.Core.Plugins.Tcp;

public class TcpPlugin : IPlugin
{
    public string Name => "Tcp";

    public void RegesterSteps(PluginHost pluginHost)
    {
        pluginHost.RegesterStep(() => new DetactionTcpStep());
    }
}