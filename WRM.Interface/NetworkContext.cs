namespace WRM.Interface;

public sealed class NetworkContext(IConnection connection)
{
    public ILoger? Loger { get; set; }
    public IConnection Connection { get; set; } = connection;
    public IDictionary<string, object> Items { get; private init; } = new Dictionary<string, object>();
    public CancellationToken Cancellation { get; init; }
}