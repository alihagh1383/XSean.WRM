using System.Net;
using System.Net.Sockets;
using WRM.Core.Interface;

namespace WRM.Core.Connections;

public sealed class TcpConnection : IConnection
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;

    public TcpConnection(Socket socket)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));

        _stream = new NetworkStream(
            socket,
            ownsSocket: true
        );
    }

    public Stream Stream => _stream;

    public EndPoint RemoteEndPoint => _socket.RemoteEndPoint!;

    public void Dispose()
    {
        try { _stream.Dispose(); }
        catch { /* ignore */ }
    }
}
