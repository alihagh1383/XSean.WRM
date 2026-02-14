using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using WRM.Core;
using WRM.Core.Plugins.Http.Abstraction;
using WRM.Core.Plugins.Http.Http1;
using WRM.Core.Plugins.Http.HttpPipline;
using WRM.Core.Plugins.Http.HttpPipline.Middlewares.QueryParams;
using WRM.Core.Plugins.Http.HttpPipline.Middlewares.Routing;
using WRM.Core.Plugins.Http.HttpPipline.Middlewares.Routing.Abstraction;
using WRM.Core.Plugins.ProtocolDetection;
using WRM.Core.Plugins.Tcp;
using WRM.Core.Plugins.TcpToSSL;
using WRM.Test;

const int maxConcurrentConnections = 100; // حداکثر تعداد connection های همزمان

/* declear */
Socket lestenerTcp;
PluginHost host;
WRMEngine engine;
int port;
IPAddress ip;
Logger logger;
CancellationTokenSource cts;
SemaphoreSlim connectionLimiter;
HttpEndpoints endpoints;
/* init */
cts = new CancellationTokenSource();
logger = new Logger();
port = 8080;
ip = IPAddress.Any;
host = new PluginHost();
endpoints = new HttpEndpoints();

endpoints.Map(RootEndPoint(), ["get"], context => context.WriteResponse(new HttpResponse(), new MemoryStream(Encoding.ASCII.GetBytes(
    $"""
     {context.Request.Method} {context.Request.Path} {context.Request.Version}
     {string.Join("\n", context.Request.Headers)}   
     """))));

endpoints.Map(TestEndPoint(), ["get"], context => context.WriteResponse(new HttpResponse(), new MemoryStream(Encoding.ASCII.GetBytes(
    $"""
     {context.Request.Path}
     {string.Join(";", context.Request.RouteVars.Select(pair => $"[{pair.Key}, {string.Join(',', pair.Value)}]"))}
     {string.Join(";", context.Request.QueryVars.Select(pair => $"[{pair.Key}, {string.Join(',', pair.Value)}]"))}
     {string.Join("\n", context.Request.Headers)} 
     """))));

host.RegesterPlugin([
    () => new TcpPlugin(),
    () => new MoveTcpToSSLPlugin(new SslServerAuthenticationOptions()
    {
        ServerCertificate = CreateSelfSignedCertificate(),
        ClientCertificateRequired = false,
        EnabledSslProtocols = SslProtocols.None | SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        ApplicationProtocols = [SslApplicationProtocol.Http11]
    }),
    () => new ProtocolDetectionPlugin(),
    () => new Http1Plugin(),
    () => new HttpPiplinePlugin([
        () => new QueryParamsMiddleware(),
        () => new RoutingMiddleware(endpoints),
        () => new TestMiddleware()
    ])
]);
engine = new WRMEngine(host, logger);

connectionLimiter = new SemaphoreSlim(maxConcurrentConnections, maxConcurrentConnections);
ThreadPool.SetMaxThreads(maxConcurrentConnections + 1, maxConcurrentConnections + 1);

lestenerTcp = new Socket(SocketType.Stream, ProtocolType.IP);
lestenerTcp.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
lestenerTcp.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
lestenerTcp.Bind(new IPEndPoint(ip, port));
lestenerTcp.Listen();


Console.CancelKeyPress += (_, e) =>
{
    cts.Cancel();
    lestenerTcp.Shutdown(SocketShutdown.Both);
    lestenerTcp.Close();
    lestenerTcp.Dispose();
    e.Cancel = true;
    Console.WriteLine("\n🛑 Shutting down gracefully...");
};

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        Socket? client;
        try
        {
            await connectionLimiter.WaitAsync(cts.Token);
            client = await lestenerTcp.AcceptAsync();

            new Thread(() =>
            {
                _ = engine.HandleAsync(client, cts.Token);
                connectionLimiter.Release();
            }).UnsafeStart();
        }
        catch (Exception e)
        {
            await logger.LogError(null, e.Message);
        }
    }
}
catch (Exception e)
{
    await logger.LogError(null, e.Message);
}

return;


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
    var request = new CertificateRequest("CN=localhost", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);

    // Key usage
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

    // Enhanced key usage (server authentication)
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new System.Security.Cryptography.OidCollection { new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1") }, false));

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

internal partial class Program
{
    [GeneratedRegex("^/$")]
    private static partial Regex RootEndPoint();

    [GeneratedRegex("^/(?<Name>.+)$")]
    private static partial Regex TestEndPoint();
}