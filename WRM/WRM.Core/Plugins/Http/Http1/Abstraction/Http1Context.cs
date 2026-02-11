using System.Text;
using WRM.Core.Plugins.Http.Abstraction;

namespace WRM.Core.Plugins.Http.Http1.Abstraction;

public class Http1Context(bool keepAlive, int defaultKeepAliveTimeout) : HttpContext
{
    public bool KeepAlive = keepAlive;

    public override async Task WriteResponse(HttpResponse response, Stream? body, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        Console.WriteLine("Send Response");
        var version = Request.Version;
        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? GetReasonPhrase(response.StatusCode)
            : response.ReasonPhrase;

        sb.Append($"{version} {response.StatusCode} {reason}\r\n");


        bool hasContentLength = false;
        bool hasConnection = false;
        bool hasDate = false;
        bool hasServer = false;
        bool hasTransferEncoding = false;

        foreach (var header in response.Hreaders)
        {
            if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                hasContentLength = true;
            if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                hasConnection = true;
            if (header.Key.Equals("Date", StringComparison.OrdinalIgnoreCase))
                hasDate = true;
            if (header.Key.Equals("Server", StringComparison.OrdinalIgnoreCase))
                hasServer = true;
            if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                hasTransferEncoding = true;

            sb.Append($"{header.Key}: {header.Value}\r\n");
        }

        bool isHead = Request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);

        if (!hasContentLength && !hasTransferEncoding)
        {
            if (body == null)
            {
                sb.Append("Content-Length: 0\r\n");
            }
            else if (body.CanSeek)
            {
                sb.Append($"Content-Length: {body.Length}\r\n");
            }
            else
            {
                KeepAlive = false;
            }
        }

        if (!hasConnection)
        {
            sb.Append(KeepAlive
                ? "Connection: keep-alive\r\n"
                : "Connection: close\r\n");

            if (KeepAlive)
            {
                sb.Append($"Keep-Alive: timeout={defaultKeepAliveTimeout}, max=100\r\n");
            }
        }

        if (!hasDate)
        {
            sb.Append($"Date: {DateTime.UtcNow:R}\r\n");
        }

        if (!hasServer)
        {
            sb.Append("Server: WRM/1.0\r\n");
        }

        sb.Append("\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
        await Connection.WriteAsync(headerBytes, ct);
        if (!isHead && body != null)
        {
            await body.CopyToAsync(Connection, 8192, ct);
        }

        await Connection.FlushAsync(ct);
        await DisposeAsync();
    }

    public override Task MakeTunel(HttpResponse response, out Stream read, out Stream write, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var version = Request.Version;
        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? GetReasonPhrase(response.StatusCode)
            : response.ReasonPhrase;
        
        sb.Append($"{version} {response.StatusCode} Connection Established\r\n");
        bool hasDate = false;
        bool hasServer = false;
        
        foreach (var header in response.Hreaders)
        {
            if (header.Key.Equals("Date", StringComparison.OrdinalIgnoreCase))
                hasDate = true;
            if (header.Key.Equals("Server", StringComparison.OrdinalIgnoreCase))
                hasServer = true;
        
            sb.Append($"{header.Key}: {header.Value}\r\n");
        }
        
        if (!hasDate)
            sb.Append($"Date: {DateTime.UtcNow:R}\r\n");
        if (!hasServer)
            sb.Append("Server: WRM/1.0\r\n");

        sb.Append("\r\n");
        
        var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
        Connection.WriteAsync(headerBytes, ct);

        read = Connection;
        write = Connection;
        KeepAlive = false;
        return Task.CompletedTask;
    }


    private static string GetReasonPhrase(int statusCode) =>
        statusCode switch
        {
            // 1xx
            100 => "Continue",
            101 => "Switching Protocols",
            102 => "Processing",
            103 => "Early Hints",

            // 2xx
            200 => "OK",
            201 => "Created",
            202 => "Accepted",
            203 => "Non-Authoritative Information",
            204 => "No Content",
            205 => "Reset Content",
            206 => "Partial Content",

            // 3xx
            300 => "Multiple Choices",
            301 => "Moved Permanently",
            302 => "Found",
            303 => "See Other",
            304 => "Not Modified",
            305 => "Use Proxy",
            307 => "Temporary Redirect",
            308 => "Permanent Redirect",

            // 4xx
            400 => "Bad Request",
            401 => "Unauthorized",
            402 => "Payment Required",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            406 => "Not Acceptable",
            407 => "Proxy Authentication Required",
            408 => "Request Timeout",
            409 => "Conflict",
            410 => "Gone",
            411 => "Length Required",
            412 => "Precondition Failed",
            413 => "Payload Too Large",
            414 => "URI Too Long",
            415 => "Unsupported Media Type",
            416 => "Range Not Satisfiable",
            417 => "Expectation Failed",
            418 => "I'm a teapot",
            421 => "Misdirected Request",
            422 => "Unprocessable Content",
            423 => "Locked",
            424 => "Failed Dependency",
            425 => "Too Early",
            426 => "Upgrade Required",
            428 => "Precondition Required",
            429 => "Too Many Requests",
            431 => "Request Header Fields Too Large",
            451 => "Unavailable For Legal Reasons",

            // 5xx
            500 => "Internal Server Error",
            501 => "Not Implemented",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            505 => "HTTP Version Not Supported",
            506 => "Variant Also Negotiates",
            507 => "Insufficient Storage",
            508 => "Loop Detected",
            510 => "Not Extended",
            511 => "Network Authentication Required",

            _ => string.Empty
        };
}