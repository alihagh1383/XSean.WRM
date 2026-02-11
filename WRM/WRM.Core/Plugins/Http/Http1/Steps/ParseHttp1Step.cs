using WRM.Core.Interfaces;
using WRM.Core.Plugins.Http.Abstraction;
using WRM.Core.Plugins.Http.Http1.Abstraction;
using WRM.Core.Plugins.Http.Http1.Streams;
using WRM.Core.Plugins.ProtocolDetection.Enums;
using WRM.Core.Tools;

namespace WRM.Core.Plugins.Http.Http1.Steps;

public class ParseHttp1Step : IPluginStep
{
    private const int DefaultRequestTimeout = 30; // seconds
    private const int DefaultKeepAliveTimeout = 5; // seconds - reduced for better keep-alive handling
    private const int MaxHeaderSize = 8192; // 8KB
    private const int MaxRequestLineSize = 8192; // 8KB

    public async Task InvokeAsync(WRMContext context, Func<WRMContext, Task> next)
    {
        if (!context.Items.TryGetValue(Names.HttpProtocol, out var objectHttp)
            || objectHttp is not Protocols protocol
            || protocol is not Protocols.Http1
            || !(context.Items.TryGetValue(Names.TcpStream, out var objectStream) && objectStream is Stream stream))
        {
            await next(context);
            return;
        }

        var isSsl = context.Items.TryGetValue(Names.IsSsl, out var objectIsSsl) && objectIsSsl is true;
        var keepAlive = true;
        var requestCount = 0;
        var reader = new StreamLineReader(stream);

        while (keepAlive && !context.CancellationToken.IsCancellationRequested)
        {
            var timeout = requestCount == 0
                ? TimeSpan.FromSeconds(DefaultRequestTimeout)
                : TimeSpan.FromSeconds(DefaultKeepAliveTimeout);

            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            requestCts.CancelAfter(timeout);
            var ct = requestCts.Token;

            var requestLine = await reader.ReadLineAsync(ct);

            if (string.IsNullOrWhiteSpace(requestLine)) break;

            var parts = requestLine.Split(' ', 3);
            if (parts.Length < 3) throw new InvalidDataException($"Invalid request line: {requestLine}");

            var (method, path, virsion) = (parts[0], parts[1], parts[2]);

            if (requestLine.Length > MaxRequestLineSize) throw new InvalidDataException($"Large request line: {requestLine}");

            var request = new HttpRequest() { IsSsl = isSsl, Method = method, Path = path, Version = virsion };
            var totalHeaderSize = 0;

            while (true)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) continue;

                totalHeaderSize += line.Length + 2;
                if (totalHeaderSize > MaxHeaderSize) throw new InvalidDataException("Request headers too large");

                if (string.IsNullOrEmpty(line)) break;

                var colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                var name = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                request.Headers.Add(new KeyValuePair<string, string>(name, value));
            }

            request.Body = new HttpRequestBodyStream(stream, request.Headers.ContentLength, request.Headers.TransferEncoding);

            var httpContext = new Http1Context(keepAlive, DefaultKeepAliveTimeout) { Connection = stream, Request = request };
            requestCount++;

            var connectionHeader = httpContext.Request.Headers.Connection;
            var requestVersion = httpContext.Request.Version;
            keepAlive = requestVersion.StartsWith("HTTP/1.0")
                ? string.Equals(connectionHeader, "keep-alive", StringComparison.OrdinalIgnoreCase)
                : !string.Equals(connectionHeader, "close", StringComparison.OrdinalIgnoreCase);

            context.Items[Names.HttpContext] = httpContext;

            await next(context);

            keepAlive = keepAlive && httpContext.KeepAlive;

            await httpContext.Request.Body.DisposeAsync();
        }
        
    }
}