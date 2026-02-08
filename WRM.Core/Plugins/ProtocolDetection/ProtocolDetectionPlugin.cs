using WRM.Core.Interface;
using WRM.Core.Plugins.ProtocolDetection.Steps;

namespace WRM.Core.Plugins.ProtocolDetection;

public sealed class ProtocolDetectionPlugin : IPlugin
{
    public string Name => "ProtocolDetection";

    public void Register(IPluginHost host)
    {
        host.Use<ProtocolDetectionStep>();
    }
}
public enum DetectedProtocol
{
    Unknown,
    Http1,
    Http2,
    Tls
}
