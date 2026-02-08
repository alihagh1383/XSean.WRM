using System.Net;
using System.Net.Security;
using WRM.Core.Interface;

namespace WRM.Core.Connections;

public sealed class SslConnection : IConnection
{
    private readonly SslStream _ssl;
    private readonly IConnection _inner;

    public SslConnection(
        IConnection inner,
        SslStream ssl)
    {
        _inner = inner;
        _ssl = ssl;
    }

    public Stream Stream => _ssl;

    public EndPoint RemoteEndPoint => _inner.RemoteEndPoint;

    public void Dispose()
    {
        try { _ssl.Dispose(); }
        finally { _inner.Dispose(); }
    }
}
