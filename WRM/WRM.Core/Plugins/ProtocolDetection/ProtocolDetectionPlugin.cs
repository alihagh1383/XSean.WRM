using WRM.Core.Interfaces;
using WRM.Core.Plugins.ProtocolDetection.Steps;

namespace WRM.Core.Plugins.ProtocolDetection;

public class ProtocolDetectionPlugin : IPlugin
{
    public string Name => "Protocol Detection";

    public void RegesterSteps(PluginHost pluginHost)
    {
        pluginHost.RegesterStep(() => new ProtocolDetectionStep());
    }
}