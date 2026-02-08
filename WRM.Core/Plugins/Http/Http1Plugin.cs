using WRM.Core.Interface;
using WRM.Core.Plugins.Http.Steps;

namespace WRM.Core.Plugins.Http;

public sealed class Http1Plugin : IPlugin
{
    public string Name => "HTTP/1";

    public void Register(IPluginHost host)
    {
        host.Use<Http1ParsingStep>();
    }
}
