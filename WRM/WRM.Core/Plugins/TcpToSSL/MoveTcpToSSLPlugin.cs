using System.Net.Security;
using WRM.Core.Interfaces;
using WRM.Core.Plugins.TcpToSSL.Steps;

namespace WRM.Core.Plugins.TcpToSSL;

public class MoveTcpToSSLPlugin(SslServerAuthenticationOptions options) : IPlugin
{
    public string Name => "Move Tcp To SSL";
    public void RegesterSteps(PluginHost pluginHost)
    {
        pluginHost.RegesterStep(() => new SSLDetectionStep());
        pluginHost.RegesterStep(() => new MoveToSSLStep(options));
    }
}