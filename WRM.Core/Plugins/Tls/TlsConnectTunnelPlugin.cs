using WRM.Core.Interface;
using WRM.Core.Plugins.Tls.Steps;

namespace WRM.Core.Plugins.Tls;

public sealed class TlsConnectTunnelPlugin : IPlugin
{
    public string Name => "TLS PassThrough";

    public void Register(IPluginHost host)
    {
        host.Use<TlsConnectTunnelStep>();
    }
}
