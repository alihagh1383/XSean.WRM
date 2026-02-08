using System.Net;

namespace WRM.Core.Interface;

public interface IConnection : IDisposable
{
    Stream Stream { get; }
    EndPoint RemoteEndPoint { get; }
}
