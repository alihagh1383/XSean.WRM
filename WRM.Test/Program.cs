using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using WRM.Core;
using WRM.HTTP.HTTP1;
using WRM.HTTP.HTTP2;
using WRM.HTTP.ProtocolDetection;
using WRM.SSL;
using WRM.Tcp.Connections;
using WRM.Test;

// ساخت و ثبت plugin‌ها
var host = new PluginHost();
var loader = new PluginLoader(host);

loader.Load([
    new SSLPlugin(new SslServerAuthenticationOptions()
    {
        ServerCertificate = CreateSelfSignedCertificate(),
        ClientCertificateRequired = false,
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        ApplicationProtocols = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11]
    }),
    new ProtocolDetectionPlugin(),
    new Http1Plugin(),
    new Http2Plugin(),
    new TestPlugin()
]);

var pipeline = PipelineFactory.Create(host);
var engine = new NetworkEngine(pipeline, new ConsoleLoger());

// Configuration
const int maxConcurrentConnections = 100; // حداکثر تعداد connection های همزمان
const int port = 8080;

// Semaphore برای محدود کردن تعداد connection های همزمان
using var connectionLimiter = new SemaphoreSlim(maxConcurrentConnections, maxConcurrentConnections);
ThreadPool.SetMaxThreads(maxConcurrentConnections + 1, maxConcurrentConnections + 1);
// CancellationToken برای shutdown
using var cts = new CancellationTokenSource();

// HTTP Listener
Socket httpListener = new(SocketType.Stream, ProtocolType.IP);

// Configure socket options for better keep-alive handling
httpListener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
httpListener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

httpListener.Bind(new IPEndPoint(IPAddress.Loopback, port));
httpListener.Listen(); // Increased from 100 to 511 (common max backlog)

Console.WriteLine($"🚀 Server started on http://localhost:{port}");
Console.WriteLine($"📊 Max concurrent connections: {maxConcurrentConnections}");
Console.WriteLine("Press Ctrl+C to stop...\n");

// آمار
var stats = new ServerStats();

// Graceful shutdown handler
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\n🛑 Shutting down gracefully...");
    cts.Cancel();
    httpListener.Close();
};

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        Socket? socket = null;

        try
        {
            socket = await httpListener.AcceptAsync(cts.Token);

            stats.IncrementTotal();
            new Thread(() => _ = HandleConnectionAsync(socket, engine, connectionLimiter, stats, cts.Token)).UnsafeStart();
        }
        catch (OperationCanceledException)
        {
            socket?.Close();
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error accepting connection: {ex.Message}");
            socket?.Close();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Fatal server error: {ex.Message}");
}
finally
{
    Console.WriteLine("\n📊 Final Statistics:");
    Console.WriteLine($"   Total connections: {stats.TotalConnections}");
    Console.WriteLine($"   Active connections: {stats.ActiveConnections}");
    Console.WriteLine($"   Failed connections: {stats.FailedConnections}");

    httpListener.Close();
    Console.WriteLine("✅ Server stopped");
}

return;

static async Task HandleConnectionAsync(
    Socket socket,
    NetworkEngine engine,
    SemaphoreSlim connectionLimiter,
    ServerStats stats,
    CancellationToken ct)
{
    // منتظر می‌مونیم تا slot خالی بشه
    await connectionLimiter.WaitAsync(ct);

    stats.IncrementActive();

    var remoteEndPoint = socket.RemoteEndPoint?.ToString() ?? "unknown";
    Console.WriteLine($"🔗 New connection from {remoteEndPoint} (Active: {stats.ActiveConnections})");

    try
    {
        using var conn = new TcpConnection(socket);
        await engine.HandleAsync(conn, ct);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"🔌 Connection cancelled: {remoteEndPoint}");
    }
    catch (Exception ex)
    {
        stats.IncrementFailed();
        Console.WriteLine($"❌ Connection error ({remoteEndPoint}): {ex.Message}");
    }
    finally
    {
        stats.DecrementActive();
        connectionLimiter.Release();

        try
        {
            socket.Close();
        }
        catch
        {
            // Ignore socket close errors
        }
    }
}

static X509Certificate2 CreateSelfSignedCertificate()
{
    try
    {
        if (File.Exists("server.pfx"))
        {
            Console.WriteLine("📜 Loading existing certificate from server.pfx");
            return X509CertificateLoader.LoadPkcs12FromFile("server.pfx", "password");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Could not load existing certificate: {ex.Message}");
    }

    Console.WriteLine("🔐 Creating new self-signed certificate...");

    using var rsa = System.Security.Cryptography.RSA.Create(2048);
    var request = new CertificateRequest(
        "CN=localhost",
        rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1
    );

    // Key usage
    request.CertificateExtensions.Add(
        new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            false));

    // Enhanced key usage (server authentication)
    request.CertificateExtensions.Add(
        new X509EnhancedKeyUsageExtension(
            new System.Security.Cryptography.OidCollection
            {
                new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1") // Server Authentication
            },
            false));

    // Subject Alternative Names
    var sanBuilder = new SubjectAlternativeNameBuilder();
    sanBuilder.AddDnsName("localhost");
    sanBuilder.AddIpAddress(IPAddress.Loopback);
    sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
    request.CertificateExtensions.Add(sanBuilder.Build());

    var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(5));

    try
    {
        var certBytes = certificate.Export(X509ContentType.Pfx, "password");
        File.WriteAllBytes("server.pfx", certBytes);
        Console.WriteLine("✅ Certificate saved to server.pfx");

        return X509CertificateLoader.LoadPkcs12(certBytes, "password");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Could not save certificate: {ex.Message}");
        return certificate;
    }
}

class ServerStats
{
    private int _totalConnections;
    private int _activeConnections;
    private int _failedConnections;

    public int TotalConnections => _totalConnections;
    public int ActiveConnections => _activeConnections;
    public int FailedConnections => _failedConnections;

    public void IncrementTotal() => Interlocked.Increment(ref _totalConnections);
    public void IncrementActive() => Interlocked.Increment(ref _activeConnections);
    public void DecrementActive() => Interlocked.Decrement(ref _activeConnections);
    public void IncrementFailed() => Interlocked.Increment(ref _failedConnections);
}