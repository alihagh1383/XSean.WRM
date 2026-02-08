using WRM.HTTP.HTTP2.Connection;
using WRM.HTTP.HTTP2.Frames;
using WRM.HTTP.ProtocolDetection;
using WRM.Interface;

namespace WRM.HTTP.HTTP2.Steps;

public class Http2PrefaceStep : IPipelineStep
{
    private static readonly byte[] ClientPreface =
    [
        0x50, 0x52, 0x49, 0x20, 0x2a, 0x20, 0x48, 0x54, // PRI * HT
        0x54, 0x50, 0x2f, 0x32, 0x2e, 0x30, 0x0d, 0x0a, // TP/2.0..
        0x0d, 0x0a, 0x53, 0x4d, 0x0d, 0x0a, 0x0d, 0x0a // ..SM....
    ];

    public async Task InvokeAsync(NetworkContext ctx, Func<NetworkContext, Task> next)
    {
        // بررسی که آیا پروتکل HTTP/2 تشخیص داده شده؟
        if (!ctx.Items.TryGetValue("HTTP_PROTOCOL", out var p) ||
            (DetectedProtocol)p != DetectedProtocol.Http2)
        {
            await next(ctx);
            return;
        }

        ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info, "[HTTP/2 Preface] Starting HTTP/2 handshake...");

        // ساخت connection جدید
        var connection = new Http2Connection();

        // Initialize کردن با stream از connection (که ممکنه wrapped باشه توسط ProtocolDetection)
        var networkStream = ctx.Connection.Stream;
        connection.Initialize(networkStream, isServer: true);

        try
        {
            // خواندن client preface - ProtocolDetection قبلاً خونده و با BufferedPeekStream دوباره قابل خواندنه
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info, "[HTTP/2 Preface] Reading client preface (24 bytes)...");
            byte[] prefaceBuffer = new byte[24];
            int read = await networkStream.ReadAsync(prefaceBuffer, 0, 24);

            if (read != 24)
            {
                ctx.Loger?.LogAsync(this, ILoger.LogLevel.Error, $"[HTTP/2 Preface] ❌ Expected 24 bytes, got {read}");
                throw new InvalidOperationException($"Invalid HTTP/2 client preface length: {read}");
            }

            if (!IsPrefaceValid(prefaceBuffer))
            {
                ctx.Loger?.LogAsync(this, ILoger.LogLevel.Error,"[HTTP/2 Preface] ❌ Preface validation failed");
                throw new InvalidOperationException("Invalid HTTP/2 client preface content");
            }

            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info, "[HTTP/2 Preface] ✅ Client preface validated");

            // حالا باید یک SETTINGS frame بخونیم از client
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info,"[HTTP/2 Preface] Waiting for SETTINGS frame...");
            var initialFrame = await connection.Reader!.ReadFrameAsync();

            if (initialFrame == null)
            {
                ctx.Loger?.LogAsync(this, ILoger.LogLevel.Error,"[HTTP/2 Preface] ❌ No SETTINGS frame received");
                throw new InvalidOperationException("Connection closed before SETTINGS frame");
            }

            if (initialFrame.Type != Http2FrameType.Settings)
            {
                ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info,$"[HTTP/2 Preface] ❌ Expected SETTINGS, got {initialFrame.Type}");
                throw new InvalidOperationException($"Expected SETTINGS frame after preface, got {initialFrame.Type}");
            }

            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info,"[HTTP/2 Preface] ✅ Received SETTINGS frame");

            // Parse و اعمال settings
            var clientSettings = SettingsFrame.Parse(initialFrame);
            clientSettings.ApplyTo(connection.RemoteSettings);
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info,$"[HTTP/2 Preface] ✅ Applied client settings");

            // ارسال SETTINGS خودمون
            var ourSettings = new SettingsFrame
            {
                Parameters = new Dictionary<ushort, uint>
                {
                    { SettingsFrame.SETTINGS_MAX_CONCURRENT_STREAMS, connection.LocalSettings.MaxConcurrentStreams },
                    { SettingsFrame.SETTINGS_INITIAL_WINDOW_SIZE, connection.LocalSettings.InitialWindowSize },
                    { SettingsFrame.SETTINGS_MAX_FRAME_SIZE, connection.LocalSettings.MaxFrameSize }
                }
            };

            await connection.Writer!.WriteFrameAsync(ourSettings.ToFrame());
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info,"[HTTP/2 Preface] ✅ Sent our SETTINGS frame");

            // ارسال SETTINGS ACK برای client settings
            await connection.Writer.WriteFrameAsync(new SettingsFrame().ToFrame(ack: true));
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info,"[HTTP/2 Preface] ✅ Sent SETTINGS ACK");

            // منتظر SETTINGS ACK از client
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info,"[HTTP/2 Preface] Waiting for SETTINGS ACK...");
            var ackFrame = await connection.Reader.ReadFrameAsync();

            if (ackFrame == null)
            {
                ctx.Loger?.LogAsync(this, ILoger.LogLevel.Error,"[HTTP/2 Preface] ❌ No SETTINGS ACK received");
                throw new InvalidOperationException("Connection closed before SETTINGS ACK");
            }

            // if (ackFrame.Type != Http2FrameType.Settings || !ackFrame.Ack)
            // {
            //     Console.WriteLine($"[HTTP/2 Preface] ❌ Expected SETTINGS ACK, got {ackFrame.Type} (ACK={ackFrame.Ack})");
            //     throw new InvalidOperationException("Expected SETTINGS ACK from client");
            // }
            // Console.WriteLine("[HTTP/2 Preface] ✅ Received SETTINGS ACK");

            connection.HandshakeComplete = true;
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info,"[HTTP/2 Preface] 🎉 HTTP/2 handshake complete!");

            // ذخیره connection در context
            ctx.Items["http2"] = connection;
        }
        catch (Exception ex)
        {
            ctx.Loger?.LogAsync(this, ILoger.LogLevel.Error,$"[HTTP/2 Preface] ❌ Handshake failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                ctx.Loger?.LogAsync(this, ILoger.LogLevel.Error,$"[HTTP/2 Preface]    Inner: {ex.InnerException.Message}");
            }

            throw new InvalidOperationException("HTTP/2 handshake failed", ex);
        }

        await next(ctx);
    }

    private static bool IsPrefaceValid(byte[] preface)
    {
        if (preface.Length != ClientPreface.Length) return false;

        return !ClientPreface.Where((t, i) => preface[i] != t).Any();
    }
}