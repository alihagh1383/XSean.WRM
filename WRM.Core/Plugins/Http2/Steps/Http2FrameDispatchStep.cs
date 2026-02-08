using System;
using System.Threading.Tasks;
using WRM.Core.Interface;
using WRM.Core.Plugins.Http2.Connection;
using WRM.Core.Plugins.Http2.Frames;
using WRM.Core.Plugins.Http2.Stream;

namespace WRM.Core.Plugins.Http2.Steps;

/// <summary>
/// خواندن و dispatch کردن فریم‌های HTTP/2
/// </summary>
public class Http2FrameDispatchStep : IPipelineStep
{
    public async Task InvokeAsync(NetworkContext ctx, Func<NetworkContext,Task> next)
    {
        // بررسی که آیا HTTP/2 connection داریم
        if (!ctx.Items.TryGetValue("http2", out var connObj) || connObj is not Http2Connection connection)
        {
            await next(ctx);
            return;
        }

        if (!connection.HandshakeComplete)
        {
            await next(ctx);
            return;
        }

        try
        {
            // خواندن فریم‌ها تا زمانی که connection باز هست
            while (true)
            {
                var frame = await connection.Reader!.ReadFrameAsync();
                
                if (frame == null)
                {
                    await next(ctx);
                    break;
                }

                // Dispatch بر اساس نوع فریم
                await DispatchFrameAsync(connection, frame, ctx);
                await next(ctx);
            }
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error in frame dispatch: {ex.Message}");
            throw;
        }

    }

    private async Task DispatchFrameAsync(Http2Connection connection, Http2Frame frame, NetworkContext ctx)
    {
        switch (frame.Type)
        {
            case Http2FrameType.Data:
                await HandleDataFrameAsync(connection, frame);
                break;

            case Http2FrameType.Headers:
                await HandleHeadersFrameAsync(connection, frame);
                break;

            case Http2FrameType.Settings:
                await HandleSettingsFrameAsync(connection, frame);
                break;

            case Http2FrameType.Ping:
                await HandlePingFrameAsync(connection, frame);
                break;

            case Http2FrameType.WindowUpdate:
                await HandleWindowUpdateFrameAsync(connection, frame);
                break;

            case Http2FrameType.RstStream:
                await HandleRstStreamFrameAsync(connection, frame);
                break;

            case Http2FrameType.GoAway:
                await HandleGoAwayFrameAsync(connection, frame);
                break;

            case Http2FrameType.Priority:
                // Priority handling - فعلاً skip می‌کنیم
                break;

            default:
                // Unknown frame type - ignore طبق spec
                Console.WriteLine($"Unknown frame type: {frame.Type}");
                break;
        }
    }

    private async Task HandleDataFrameAsync(Http2Connection connection, Http2Frame frame)
    {
        var dataFrame = DataFrame.Parse(frame);
        var stream = connection.GetOrCreateStream(frame.StreamId);

        // بررسی stream state
        if (stream.State != Http2StreamState.Open && stream.State != Http2StreamState.HalfClosedLocal)
        {
            // Stream در وضعیت نامعتبر - باید RST_STREAM بفرستیم
            await SendRstStreamAsync(connection, frame.StreamId, 0x5); // STREAM_CLOSED
            return;
        }

        // ذخیره data در stream body
        if (stream.Body == null)
        {
            stream.Body = new System.IO.MemoryStream();
        }
        
        await stream.Body.WriteAsync(dataFrame.Data, 0, dataFrame.Data.Length);

        // اگه END_STREAM بود، stream state رو تغییر بده
        if (dataFrame.EndStream)
        {
            stream.EndStreamReceived = true;
            
            stream.State = stream.State == Http2StreamState.HalfClosedLocal 
                ? Http2StreamState.Closed 
                : Http2StreamState.HalfClosedRemote;
                
            stream.Body.Position = 0; // Reset برای خواندن
        }
    }

    private async Task HandleHeadersFrameAsync(Http2Connection connection, Http2Frame frame)
    {
        // Parse با HPACK decoder
        var headersFrame = HeadersFrame.Parse(frame, connection.Decoder);
        var stream = connection.GetOrCreateStream(frame.StreamId);
        
        if (stream.State == Http2StreamState.Idle)
        {
            stream.State = Http2StreamState.Open;
        }

        // استخراج pseudo-headers و regular headers
        string method = "";
        string path = "";
        string authority = "";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in headersFrame.Headers)
        {
            if (name.StartsWith(":"))
            {
                // Pseudo-headers
                switch (name)
                {
                    case ":method":
                        method = value;
                        break;
                    case ":path":
                        path = value;
                        break;
                    case ":authority":
                        authority = value;
                        break;
                }
            }
            else
            {
                // Regular headers
                headers[name] = value;
            }
        }

        // اضافه کردن authority به عنوان Host header
        if (!string.IsNullOrEmpty(authority))
        {
            headers["Host"] = authority;
        }

        // ساخت HttpRequest
        stream.Request = new Http.Classes.HttpRequest
        {
            Method = method,
            Path = path,
            Version = "HTTP/2.0"
        };

        // کپی headers
        foreach (var (name, value) in headers)
        {
            stream.Request.Headers[name] = value;
        }

        stream.HeadersReceived = true;

        if (frame.EndStream)
        {
            stream.EndStreamReceived = true;
            stream.State = Http2StreamState.HalfClosedRemote;
        }

        Console.WriteLine($"Received HEADERS on stream {frame.StreamId}: {method} {path}");
    }

    private async Task HandleSettingsFrameAsync(Http2Connection connection, Http2Frame frame)
    {
        if (frame.Ack)
        {
            // Settings ACK - no action needed
            Console.WriteLine("[HTTP/2 Preface] ✅ Received SETTINGS ACK");
            return;
        }

        var settings = SettingsFrame.Parse(frame);
        settings.ApplyTo(connection.RemoteSettings);

        // ارسال ACK
        await connection.Writer!.WriteFrameAsync(new SettingsFrame().ToFrame(ack: true));
    }

    private async Task HandlePingFrameAsync(Http2Connection connection, Http2Frame frame)
    {
        if (frame.Ack)
        {
            // PING ACK - no action needed
            return;
        }

        // ارسال PING ACK با همون payload
        var pongFrame = new Http2Frame
        {
            Type = Http2FrameType.Ping,
            StreamId = 0,
            Flags = 0x1, // ACK
            Length = frame.Length,
            Payload = frame.Payload
        };

        await connection.Writer!.WriteFrameAsync(pongFrame);
    }

    private async Task HandleWindowUpdateFrameAsync(Http2Connection connection, Http2Frame frame)
    {
        // TODO: Implement flow control
        Console.WriteLine($"Received WINDOW_UPDATE on stream {frame.StreamId}");
    }

    private async Task HandleRstStreamFrameAsync(Http2Connection connection, Http2Frame frame)
    {
        var stream = connection.GetOrCreateStream(frame.StreamId);
        stream.State = Http2StreamState.Closed;
        
        // حذف stream
        connection.RemoveStream(frame.StreamId);
    }

    private async Task HandleGoAwayFrameAsync(Http2Connection connection, Http2Frame frame)
    {
        // Connection داره بسته می‌شه
        Console.WriteLine("Received GOAWAY frame");
        // TODO: Graceful shutdown
    }

    private async Task SendRstStreamAsync(Http2Connection connection, int streamId, uint errorCode)
    {
        var payload = new byte[4];
        payload[0] = (byte)(errorCode >> 24);
        payload[1] = (byte)(errorCode >> 16);
        payload[2] = (byte)(errorCode >> 8);
        payload[3] = (byte)errorCode;

        var rstFrame = new Http2Frame
        {
            Type = Http2FrameType.RstStream,
            StreamId = streamId,
            Length = 4,
            Payload = payload
        };

        await connection.Writer!.WriteFrameAsync(rstFrame);
    }
}
