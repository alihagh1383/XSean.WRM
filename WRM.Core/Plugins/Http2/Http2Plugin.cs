using WRM.Core.Interface;
using WRM.Core.Plugins.Http2.Steps;

namespace WRM.Core.Plugins.Http2;

public class Http2Plugin : IPlugin
{
    public string Name => "HTTP/2";

    public void Register(IPluginHost host)
    {
        host.Use<Http2PrefaceStep>();
        host.Use<Http2FrameDispatchStep>();
        host.Use<Http2RequestStep>();
    }
}