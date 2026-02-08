namespace WRM.Core.Interface;

public interface INetworkEngine
{
    Task HandleAsync(IConnection connection, CancellationToken ct);
}