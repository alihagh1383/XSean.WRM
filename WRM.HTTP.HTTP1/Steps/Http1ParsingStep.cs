using System.Text;
using WRM.HTTP.HTTP1.Streams;
using WRM.HTTP.ProtocolDetection;
using WRM.Interface;
using WRM.Tools.Streams;

namespace WRM.HTTP.HTTP1.Steps;

/// <summary>
/// HTTP/1.1 parsing step با پشتیبانی کامل از keep-alive و مدیریت همزمانی
/// </summary>
public sealed class Http1ParsingStep : IPipelineStep
{
    private const int DefaultRequestTimeout = 30; // seconds
    private const int DefaultKeepAliveTimeout = 5; // seconds - reduced for better keep-alive handling
    private const int MaxHeaderSize = 8192; // 8KB

    public async Task InvokeAsync(
        NetworkContext ctx,
        Func<NetworkContext, Task> next)
    {
        if (!ctx.Items.TryGetValue("HTTP_PROTOCOL", out var p) ||
            (DetectedProtocol)p != DetectedProtocol.Http1)
        {
            await next(ctx);
            return;
        }

        Stream stream = ctx.Connection.Stream;
        bool keepAlive = true;
        int requestCount = 0;

        try
        {
            // حلقه برای مدیریت multiple requests روی یک connection (HTTP/1.1 keep-alive)
            while (keepAlive && !ctx.Cancellation.IsCancellationRequested)
            {
                // Limit requests per connection to prevent resource exhaustion

                HttpContext? context = null;

                try
                {
                    // اگر اولین request نیست، timeout کوتاه‌تری برای keep-alive داریم
                    var timeout = requestCount == 0
                        ? TimeSpan.FromSeconds(DefaultRequestTimeout)
                        : TimeSpan.FromSeconds(DefaultKeepAliveTimeout);

                    using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.Cancellation);
                    requestCts.CancelAfter(timeout);

                    context = await ReadRequestAsync(stream, requestCts.Token);

                    if (context == null)
                    {
                        break;
                    }

                    requestCount++;

                    // تشخیص اینکه client می‌خواد connection رو نگه داره یا نه
                    var connectionHeader = GetHeader(context.Request.Headers, "Connection");
                    var requestVersion = context.Request.Version;

                    // HTTP/1.0 به صورت پیش‌فرض keep-alive نداره مگر اینکه صریحاً گفته شه
                    // HTTP/1.1 به صورت پیش‌فرض keep-alive داره مگر اینکه "close" گفته شه
                    if (requestVersion.StartsWith("HTTP/1.0"))
                    {
                        keepAlive = string.Equals(connectionHeader, "keep-alive",
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else // HTTP/1.1 or higher
                    {
                        keepAlive = !string.Equals(connectionHeader, "close",
                            StringComparison.OrdinalIgnoreCase);
                    }

                    // CRITICAL: Consume the entire request body before processing
                    if (context.Request.Body != null)
                    {
                        try
                        {
                            await ConsumeRequestBodyAsync(context.Request.Body, requestCts.Token);
                        }
                        catch (Exception ex)
                        {
                            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Error, $"⚠️ Failed to consume request body: {ex.Message}");
                            keepAlive = false;
                        }
                    }

                    // اضافه کردن context به items برای middleware بعدی
                    ctx.Items["HTTP_CONTEXT"] = context;

                    // فراخوانی middleware بعدی (مثل TestPlugin)
                    await next(ctx);

                    // اگر response تنظیم نشده، یک 500 برمیگردونیم
                    context.Response ??= new HttpResponse
                    {
                        StatusCode = 500,
                        StatusDescription = "Internal Server Error"
                    };

                    // نوشتن response به client
                    await WriteResponseAsync(context, stream, keepAlive, ctx.Cancellation);
                    ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info, $"HTTP handler start {Thread.CurrentThread.ManagedThreadId}");
                    ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info, $"✅ {context.Request.Method} {context.Request.Path} -> {context.Response.StatusCode} (Request #{requestCount}, KeepAlive: {keepAlive})");
                }
                catch (OperationCanceledException) when (!ctx.Cancellation.IsCancellationRequested)
                {
                    // Timeout - اگر timeout شد و اولین request نبود، عادیه (keep-alive timeout)
                    if (requestCount > 0)
                    {
                        ctx.Loger?.LogAsync(this, ILoger.LogLevel.Warn, $"⏱️ Keep-alive timeout after {requestCount} requests");
                    }
                    else
                    {
                        ctx.Loger?.LogAsync(this, ILoger.LogLevel.Warn, "⏱️ Request timeout");
                        await SendTimeoutResponse(stream, ctx.Cancellation);
                    }

                    break;
                }
                catch (IOException ex)
                {
                    // Connection closed by client - this is normal for keep-alive
                    ctx.Loger?.LogAsync(this, (keepAlive) ? ILoger.LogLevel.Info : ILoger.LogLevel.Warn, $"🔌 Connection closed by client: {ex.Message}");
                    break;
                }
                catch (InvalidDataException ex)
                {
                    // Bad request - داده‌های نامعتبر
                    ctx.Loger?.LogAsync(this, ILoger.LogLevel.Warn, $"❌ Bad request: {ex.Message}");
                    await SendErrorResponse(stream, 400, "Bad Request", ctx.Cancellation);
                    break;
                }
                catch (Exception ex)
                {
                    // خطای داخلی
                    ctx.Loger?.LogAsync(this, ILoger.LogLevel.Error, $"❌ Internal error: {ex.Message}");

                    if (context != null && !ctx.Cancellation.IsCancellationRequested)
                    {
                        try
                        {
                            await SendErrorResponse(stream, 500, "Internal Server Error", ctx.Cancellation);
                        }
                        catch
                        {
                            /* ignore */
                        }
                    }

                    break;
                }
                finally
                {
                    // Cleanup response body but DON'T close the stream
                    if (context?.Response?.Body != null)
                    {
                        try
                        {
                            await context.Response.Body.DisposeAsync();
                        }
                        catch
                        {
                            /* ignore */
                        }
                    }
                }
            }
        }
        finally
        {
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info, $"🔌 Closing connection ({requestCount} requests served)");

            try
            {
                await stream.FlushAsync(ctx.Cancellation);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    /// <summary>
    /// Consume and discard request body to prepare for next request in keep-alive
    /// </summary>
    private async Task ConsumeRequestBodyAsync(Stream? body, CancellationToken ct)
    {
        if (body is not { CanRead: true })
            return;

        var buffer = new byte[8192];
        int bytesRead;

        do
        {
            bytesRead = await body.ReadAsync(buffer, 0, buffer.Length, ct);
        } while (bytesRead > 0);
    }

    /// <summary>
    /// خواندن یک HTTP request از stream
    /// </summary>
    private async Task<HttpContext?> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        var reader = new StreamLineReader(stream);

        try
        {
            // خواندن request line
            var requestLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(requestLine))
                return null; // Connection closed gracefully

            // Parse request line: "GET /path HTTP/1.1"
            var parts = requestLine.Split(' ', 3);
            if (parts.Length < 3)
                throw new InvalidDataException($"Invalid request line: {requestLine}");

            var request = new HttpRequest
            {
                Method = parts[0],
                Path = parts[1],
                Version = parts[2]
            };

            // خواندن headers
            int totalHeaderSize = requestLine.Length + 2; // +2 for CRLF

            while (true)
            {
                var line = await reader.ReadLineAsync(ct);

                if (line == null) continue;
                totalHeaderSize += line.Length + 2;
                if (totalHeaderSize > MaxHeaderSize)
                    throw new InvalidDataException("Request headers too large");

                if (string.IsNullOrEmpty(line))
                    break; // End of headers

                var colonIndex = line.IndexOf(':');
                if (colonIndex <= 0)
                    continue; // Invalid header, skip

                var name = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                request.Headers.Add(new KeyValuePair<string, string>(name, value));
            }

            // ساخت request body stream
            var contentLength = GetHeader(request.Headers, "Content-Length");
            var transferEncoding = GetHeader(request.Headers, "Transfer-Encoding");

            request.Body = new HttpRequestBodyStream(stream, contentLength, transferEncoding);

            var httpContext = new HttpContext
            {
                Request = request,
                Response = new HttpResponse() // Initialize empty response
            };

            return httpContext;
        }
        catch (IOException)
        {
            // Connection closed during header read
            return null;
        }
    }

    /// <summary>
    /// نوشتن HTTP response به stream
    /// </summary>
    private async Task WriteResponseAsync(
        HttpContext context,
        Stream stream,
        bool keepAlive,
        CancellationToken ct)
    {
        var response = context.Response!;
        var sb = new StringBuilder();

        // Status line
        sb.Append($"{context.Request.Version} {response.StatusCode} {response.StatusDescription}\r\n");

        // اضافه کردن header های اجباری
        bool hasContentLength = false;
        bool hasConnection = false;
        bool hasDate = false;
        bool hasServer = false;

        foreach (var header in response.Headers)
        {
            if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                hasContentLength = true;
            if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                hasConnection = true;
            if (header.Key.Equals("Date", StringComparison.OrdinalIgnoreCase))
                hasDate = true;
            if (header.Key.Equals("Server", StringComparison.OrdinalIgnoreCase))
                hasServer = true;

            sb.Append($"{header.Key}: {header.Value}\r\n");
        }

        // اگر Content-Length نداریم و body موجوده و seekable هست، اضافه میکنیم
        if (!hasContentLength && response.Body != null)
        {
            if (response.Body.CanSeek)
            {
                var length = response.Body.Length;
                sb.Append($"Content-Length: {length}\r\n");
            }
            else
            {
                // اگر نمیتونیم طول رو تشخیص بدیم، باید connection رو ببندیم
                keepAlive = false;
            }
        }
        else if (!hasContentLength)
        {
            // No body = Content-Length: 0
            sb.Append("Content-Length: 0\r\n");
        }

        // Connection header
        if (!hasConnection)
        {
            sb.Append(keepAlive ? "Connection: keep-alive\r\n" : "Connection: close\r\n");
        }

        // Keep-Alive header for explicit timeout
        if (keepAlive && !hasConnection)
        {
            sb.Append($"Keep-Alive: timeout={DefaultKeepAliveTimeout}, max=100\r\n");
        }

        // Date header (RFC 7231)
        if (!hasDate)
        {
            sb.Append($"Date: {DateTime.UtcNow:R}\r\n");
        }

        // Server header
        if (!hasServer)
        {
            sb.Append("Server: WRM/1.0\r\n");
        }

        // End of headers
        sb.Append("\r\n");

        // نوشتن headers
        var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length, ct);

        // نوشتن body (اگر موجود باشه)
        if (response.Body != null && response.Body.Length > 0)
        {
            // Use smaller buffer for better responsiveness
            await response.Body.CopyToAsync(stream, 8192, ct);
        }

        // Flush to ensure data is sent immediately
        await stream.FlushAsync(ct);
    }

    /// <summary>
    /// ارسال response خطا
    /// </summary>
    private async Task SendErrorResponse(
        Stream stream,
        int statusCode,
        string statusDescription,
        CancellationToken ct)
    {
        try
        {
            var body = $"<html><body><h1>{statusCode} {statusDescription}</h1></body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            var sb = new StringBuilder();
            sb.Append($"HTTP/1.1 {statusCode} {statusDescription}\r\n");
            sb.Append($"Content-Type: text/html; charset=utf-8\r\n");
            sb.Append($"Content-Length: {bodyBytes.Length}\r\n");
            sb.Append($"Connection: close\r\n");
            sb.Append($"Date: {DateTime.UtcNow:R}\r\n");
            sb.Append($"Server: WRM/1.0\r\n");
            sb.Append("\r\n");

            var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, ct);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, ct);
            await stream.FlushAsync(ct);
        }
        catch
        {
            // اگر نتونستیم error response بفرستیم، چیزی نمیتونیم بکنیم
        }
    }

    /// <summary>
    /// ارسال timeout response
    /// </summary>
    private async Task SendTimeoutResponse(Stream stream, CancellationToken ct)
    {
        await SendErrorResponse(stream, 408, "Request Timeout", ct);
    }

    /// <summary>
    /// گرفتن یک header با case-insensitive comparison
    /// </summary>
    private string? GetHeader(ICollection<KeyValuePair<string, string>> headers, string name)
    {
        return headers
            .FirstOrDefault(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Value;
    }
}
// using System.Text;
// using WRM.HTTP.HTTP1.Streams;
// using WRM.HTTP.ProtocolDetection;
// using WRM.Interface;
// using WRM.Tools.Streams;
//
// namespace WRM.HTTP.HTTP1.Steps;
//
// public sealed class Http1ParsingStep : IPipelineStep
// {
//     public async Task InvokeAsync(
//         NetworkContext ctx,
//         Func<NetworkContext, Task> next)
//     {
//         if (!ctx.Items.TryGetValue("HTTP_PROTOCOL", out var p) ||
//             (DetectedProtocol)p != DetectedProtocol.Http1)
//         {
//             await next(ctx);
//             return;
//         }
//
//         Stream stream = ctx.Connection.Stream;
//         while (await ReadRequest(ctx.Connection.Stream, ctx.Cancellation).WaitAsync(TimeSpan.FromSeconds(60)) is { } context)
//         {
//             // var networkContext = new NetworkContext() { Connection = ctx.Connection, Cancellation = ctx.Cancellation };
//             // foreach (var item in ctx.Items) networkContext.Items.Add(item.Key, item.Value);
//             ctx.Items["HTTP_CONTEXT"] = context;
//             await next(ctx);
//
//             Console.WriteLine($"{context.Request.Method}");
//             Console.WriteLine($"{context.Response.StatusCode}");
//             await stream.WriteAsync(Encoding.ASCII.GetBytes($"{context.Request.Version} {context.Response.StatusCode} {context.Response.StatusDescription}\r\n"));
//             foreach (var header in context.Response.Headers)
//                 await stream.WriteAsync(Encoding.ASCII.GetBytes($"{header.Key}: {header.Value}\r\n"));
//             await stream.WriteAsync(Encoding.ASCII.GetBytes($"\r\n"));
//             context.Response.Body?.CopyToAsync(stream);
//         }
//
//         await ctx.Connection.Stream.DisposeAsync();
//         ctx.Connection.Dispose();
//     }
//
//     private async Task<HttpContext?> ReadRequest(Stream request, CancellationToken cancellationToken)
//     {
//         var reader = new StreamLineReader(request);
//
//         var requestLine = await reader.ReadLineAsync(cancellationToken);
//         if (string.IsNullOrWhiteSpace(requestLine))
//             return null;
//
//         var parts = requestLine.Split(' ', 3);
//         if (parts.Length < 3)
//             return null;
//
//         var req = new HttpRequest()
//         {
//             Method = parts[0],
//             Path = parts[1],
//             Version = parts[2]
//         };
//
//         while (true)
//         {
//             var line = await reader.ReadLineAsync(cancellationToken);
//             if (string.IsNullOrEmpty(line))
//                 break;
//
//             var idx = line.IndexOf(':');
//             if (idx <= 0) continue;
//
//             var name = line[..idx].Trim();
//             var value = line[(idx + 1)..].Trim();
//
//             req.Headers.Add(new(name, value));
//         }
//
//         Stream body = new HttpRequestBodyStream(request, req.Headers.FirstOrDefault(p => p.Key == "Content-Length").Value, req.Headers.FirstOrDefault(p => p.Key == "Content-Length").Value);
//
//         req.Body = body;
//
//         var httpContext = new HttpContext { Request = req, };
//         return httpContext;
//     }
// }