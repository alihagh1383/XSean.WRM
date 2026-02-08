using System.Net;
using WRM.Interface;

namespace WRM.WrappedConnection.Connections;

public sealed class WrappedConnection: IConnection
{
    private readonly Stream _stream;
    private readonly IConnection _inner;

    public WrappedConnection(IConnection inner, Stream stream)
    {
        _inner = inner;
        _stream = stream;
    }

    public Stream Stream => _stream;
    public EndPoint RemoteEndPoint => _inner.RemoteEndPoint;

    public void Dispose() => _inner.Dispose();
}
