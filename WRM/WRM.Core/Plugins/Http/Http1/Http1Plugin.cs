using WRM.Core.Interfaces;
using WRM.Core.Plugins.Http.Http1.Steps;

namespace WRM.Core.Plugins.Http.Http1;

public class Http1Plugin : IPlugin
{
    public string Name => "Http 1";

    public void RegesterSteps(PluginHost pluginHost)
    {
        pluginHost.RegesterStep(() => new ParseHttp1Step());
    }
}