using System.Net;
using System.Net.Sockets;
using WRM.Interface;

namespace WRM.Tcp.Connections;

public sealed class TcpConnection : IConnection
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;

    public TcpConnection(Socket socket)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));

        // ownsSocket: false - we'll manage socket lifetime manually to support keep-alive
        _stream = new NetworkStream(
            socket,
            ownsSocket: false
        );
    }

    public Stream Stream => _stream;

    public EndPoint RemoteEndPoint => _socket.RemoteEndPoint!;

    public void Dispose()
    {
        try 
        { 
            _stream.Dispose(); 
        }
        catch { /* ignore */ }
        
        try
        {
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            _socket.Close();
        }
        catch { /* ignore */ }
    }
}
