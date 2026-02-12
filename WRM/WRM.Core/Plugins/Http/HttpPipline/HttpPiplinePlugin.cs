using WRM.Core.Interfaces;
using WRM.Core.Plugins.Http.HttpPipline.Abstraction;
using WRM.Core.Plugins.Http.HttpPipline.Steps;

namespace WRM.Core.Plugins.Http.HttpPipline;

public class HttpPiplinePlugin(params List<Func<IHttpMiddleware>> createFuncs) : IPlugin
{
    public string Name => "Http Pipline";

    public void RegesterSteps(PluginHost pluginHost)
    {
        pluginHost.RegesterStep(() => new HttpPiplineStep(createFuncs));
    }
}