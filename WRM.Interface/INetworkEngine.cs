namespace WRM.Interface;

public interface INetworkEngine
{
    Task HandleAsync(IConnection connection, CancellationToken ct);
}