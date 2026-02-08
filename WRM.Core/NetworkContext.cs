using WRM.Core.Interface;

namespace WRM.Core;

public sealed class NetworkContext
{
    public IConnection Connection { get; set; }
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
    public CancellationToken Cancellation { get; init; }
}