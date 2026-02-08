using WRM.HTTP.ProtocolDetection.Steps;
using WRM.Interface;

namespace WRM.HTTP.ProtocolDetection;


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
}
