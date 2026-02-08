using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using WRM.Core;
using WRM.Core.Connections;
using WRM.Core.Interface;
using WRM.Core.Plugins;
using WRM.Core.Plugins.Firewall;
using WRM.Core.Plugins.Firewall.Rules;
using WRM.Core.Plugins.Http;
using WRM.Core.Plugins.Http2;
using WRM.Core.Plugins.ProtocolDetection;
using WRM.Core.Plugins.Tls;

Console.WriteLine("🚀 Starting WRM HTTP/2 Server...");

// ساخت و ثبت plugin‌ها
var host = new PluginHost();
var loader = new PluginLoader(host);

loader.Load([
    new ProtocolDetectionPlugin(),
    new Http2Plugin(),
    new Http1Plugin(),
    new FirewallPlugin([
        new BlockMethodRule("POST"),
        new BlockHostRule(new[] { "example.com", "bad.com" }),
    ]),
    new TlsConnectTunnelPlugin()
]);

var pipeline = PipelineFactory.Create(host);
var engine = new NetworkEngine(pipeline);

// ساخت certificate
var certificate = CreateSelfSignedCertificate();

using var cts = new CancellationTokenSource();

// HTTP Listener (port 8080)
Socket httpListener = new(SocketType.Stream, ProtocolType.IP);
httpListener.Bind(new IPEndPoint(IPAddress.Loopback, 8080));
httpListener.Listen(100);

// HTTPS Listener (port 8081)
Socket httpsListener = new(SocketType.Stream, ProtocolType.IP);
httpsListener.Bind(new IPEndPoint(IPAddress.Loopback, 8081));
httpsListener.Listen(100);

Console.WriteLine("✅ HTTP  Server listening on http://localhost:8080");
Console.WriteLine("✅ HTTPS Server listening on https://localhost:8081");
Console.WriteLine("   ALPN Protocols: h2, http/1.1");
Console.WriteLine("\nPress Ctrl+C to stop...\n");

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\n🛑 Shutting down...");
    cts.Cancel();
};

// HTTP Task
var httpTask = Task.Run(async () =>
{
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var socket = await httpListener.AcceptAsync(cts.Token);
            Console.WriteLine($"[HTTP] New connection from {socket.RemoteEndPoint}");

            _ = Task.Run(async () =>
            {
                try
                {
                    var conn = new TcpConnection(socket);
                    await engine.HandleAsync(conn, cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HTTP] Error: {ex.Message}");
                }
                finally
                {
                    try { socket.Close(); } catch { }
                }
            }, cts.Token);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("[HTTP] Listener stopped");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HTTP] Fatal error: {ex.Message}");
    }
    finally
    {
        httpListener.Close();
    }
}, cts.Token);

// HTTPS Task با ALPN Support
var httpsTask = Task.Run(async () =>
{
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var socket = await httpsListener.AcceptAsync(cts.Token);
            Console.WriteLine($"[HTTPS] New connection from {socket.RemoteEndPoint}");

            _ = Task.Run(async () =>
            {
                IConnection? conn = null;
                SslStream? sslStream = null;
                
                try
                {
                    conn = new TcpConnection(socket);
                    
                    // ساخت SslStream با ALPN callbacks
                    var sslOptions = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = certificate,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        
                        // 🔥 کلید اصلی: ALPN Support
                        ApplicationProtocols = new List<SslApplicationProtocol>
                        {
                            SslApplicationProtocol.Http2,      // h2
                            SslApplicationProtocol.Http11      // http/1.1
                        }
                    };

                    sslStream = new SslStream(conn.Stream, false);
                    
                    // SSL handshake
                    await sslStream.AuthenticateAsServerAsync(sslOptions, cts.Token);

                    // چک کردن negotiated protocol
                    var negotiatedProtocol = sslStream.NegotiatedApplicationProtocol;
                    Console.WriteLine($"[HTTPS] SSL handshake complete");
                    Console.WriteLine($"        Protocol: {sslStream.SslProtocol}");
                    Console.WriteLine($"        ALPN: {negotiatedProtocol}");

                    // Wrap کردن connection
                    conn = new SslConnection(conn, sslStream);
                    
                    await engine.HandleAsync(conn, cts.Token);
                }
                catch (AuthenticationException ex)
                {
                    Console.WriteLine($"[HTTPS] Authentication failed: {ex.Message}");
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[HTTPS] IO error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HTTPS] Error: {ex.Message}");
                    Console.WriteLine(ex);
                }
                finally
                {
                    try 
                    { 
                        sslStream?.Close();
                        socket.Close(); 
                    } 
                    catch { }
                }
            }, cts.Token);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("[HTTPS] Listener stopped");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HTTPS] Fatal error: {ex.Message}");
    }
    finally
    {
        httpsListener.Close();
    }
}, cts.Token);

await Task.WhenAll(httpTask, httpsTask);
Console.WriteLine("👋 Server stopped gracefully");

static X509Certificate2 CreateSelfSignedCertificate()
{
    try
    {
        if (File.Exists("server.pfx"))
        {
            Console.WriteLine("📜 Loading existing certificate from server.pfx");
            return new X509Certificate2("server.pfx", "password");
        }
    }
    catch { }

    Console.WriteLine("⚠️  No certificate found, creating self-signed certificate...");
    
    using var rsa = System.Security.Cryptography.RSA.Create(2048);
    var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
        "CN=localhost",
        rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1
    );

    // Key usage
    request.CertificateExtensions.Add(
        new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
            System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
            System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment,
            false
        )
    );

    // Enhanced key usage (server authentication)
    request.CertificateExtensions.Add(
        new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
            new System.Security.Cryptography.OidCollection 
            { 
                new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1")
            },
            false
        )
    );

    // Subject Alternative Names
    var sanBuilder = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
    sanBuilder.AddDnsName("localhost");
    sanBuilder.AddIpAddress(IPAddress.Loopback);
    sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
    request.CertificateExtensions.Add(sanBuilder.Build());

    var certificate = request.CreateSelfSigned(
        DateTimeOffset.Now.AddDays(-1),
        DateTimeOffset.Now.AddYears(5)
    );

    try
    {
        var certBytes = certificate.Export(X509ContentType.Pfx, "password");
        File.WriteAllBytes("server.pfx", certBytes);
        Console.WriteLine("✅ Certificate saved to server.pfx");
        
        // Return certificate با private key
        return new X509Certificate2(certBytes, "password");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Could not save certificate: {ex.Message}");
        return certificate;
    }
}