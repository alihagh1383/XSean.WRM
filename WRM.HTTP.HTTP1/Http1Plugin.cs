using WRM.HTTP.HTTP1.Steps;
using WRM.Interface;

namespace WRM.HTTP.HTTP1;

public sealed class Http1Plugin : IPlugin
{
    public string Name => "HTTP/1";

    public void Register(IPluginHost host)
    {
        host.Use<Http1ParsingStep>();
    }
}
