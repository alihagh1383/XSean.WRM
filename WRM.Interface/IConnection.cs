using System.Net;

namespace WRM.Interface;

public interface IConnection : IDisposable
{
    Stream Stream { get; }
    EndPoint RemoteEndPoint { get; }
}
