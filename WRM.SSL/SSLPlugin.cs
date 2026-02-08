using System.Net.Security;
using WRM.Interface;
using WRM.SSL.Steps;

namespace WRM.SSL;

public class SSLPlugin(SslServerAuthenticationOptions options) : IPlugin
{
    public string Name => "SSL From TCP Plugin";

    public void Register(IPluginHost host)
    {
        host.Use(() => new SSLDetectionStep());
        host.Use(() => new MoveToSSLStep(options));
    }
}