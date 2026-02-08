using System;
using System.Threading.Tasks;
using WRM.Core.Interface;
using WRM.Core.Plugins.Http2.Connection;
using WRM.Core.Plugins.Http2.Frames;
using WRM.Core.Plugins.ProtocolDetection;

namespace WRM.Core.Plugins.Http2.Steps;

/// <summary>
/// مدیریت connection preface و handshake اولیه HTTP/2
/// </summary>
public class Http2PrefaceStep : IPipelineStep
{
    private static readonly byte[] ClientPreface = new byte[]
    {
        0x50, 0x52, 0x49, 0x20, 0x2a, 0x20, 0x48, 0x54, // PRI * HT
        0x54, 0x50, 0x2f, 0x32, 0x2e, 0x30, 0x0d, 0x0a, // TP/2.0..
        0x0d, 0x0a, 0x53, 0x4d, 0x0d, 0x0a, 0x0d, 0x0a  // ..SM....
    };

    public async Task InvokeAsync(NetworkContext ctx, Func<NetworkContext,Task> next)
    {
        // بررسی که آیا پروتکل HTTP/2 تشخیص داده شده؟
        if (!ctx.Items.TryGetValue("protocol", out var p) ||
            (DetectedProtocol)p != DetectedProtocol.Http2)
        {
            await next(ctx);
            return;
        }

        Console.WriteLine("[HTTP/2 Preface] Starting HTTP/2 handshake...");

        // ساخت connection جدید
        var connection = new Http2Connection();
        
        // Initialize کردن با stream از connection (که ممکنه wrapped باشه توسط ProtocolDetection)
        var networkStream = ctx.Connection.Stream;
        connection.Initialize(networkStream, isServer: true);

        try
        {
            // خواندن client preface - ProtocolDetection قبلاً خونده و با BufferedPeekStream دوباره قابل خواندنه
            Console.WriteLine("[HTTP/2 Preface] Reading client preface (24 bytes)...");
            byte[] prefaceBuffer = new byte[24];
            int read = await networkStream.ReadAsync(prefaceBuffer, 0, 24);
            
            if (read != 24)
            {
                Console.WriteLine($"[HTTP/2 Preface] ❌ Expected 24 bytes, got {read}");
                throw new InvalidOperationException($"Invalid HTTP/2 client preface length: {read}");
            }

            if (!IsPrefaceValid(prefaceBuffer))
            {
                Console.WriteLine("[HTTP/2 Preface] ❌ Preface validation failed");
                throw new InvalidOperationException("Invalid HTTP/2 client preface content");
            }
            Console.WriteLine("[HTTP/2 Preface] ✅ Client preface validated");

            // حالا باید یک SETTINGS frame بخونیم از client
            Console.WriteLine("[HTTP/2 Preface] Waiting for SETTINGS frame...");
            var initialFrame = await connection.Reader!.ReadFrameAsync();
            
            if (initialFrame == null)
            {
                Console.WriteLine("[HTTP/2 Preface] ❌ No SETTINGS frame received");
                throw new InvalidOperationException("Connection closed before SETTINGS frame");
            }

            if (initialFrame.Type != Http2FrameType.Settings)
            {
                Console.WriteLine($"[HTTP/2 Preface] ❌ Expected SETTINGS, got {initialFrame.Type}");
                throw new InvalidOperationException($"Expected SETTINGS frame after preface, got {initialFrame.Type}");
            }
            Console.WriteLine("[HTTP/2 Preface] ✅ Received SETTINGS frame");

            // Parse و اعمال settings
            var clientSettings = SettingsFrame.Parse(initialFrame);
            clientSettings.ApplyTo(connection.RemoteSettings);
            Console.WriteLine($"[HTTP/2 Preface] ✅ Applied client settings");

            // ارسال SETTINGS خودمون
            var ourSettings = new SettingsFrame
            {
                Parameters = new()
                {
                    { SettingsFrame.SETTINGS_MAX_CONCURRENT_STREAMS, connection.LocalSettings.MaxConcurrentStreams },
                    { SettingsFrame.SETTINGS_INITIAL_WINDOW_SIZE, connection.LocalSettings.InitialWindowSize },
                    { SettingsFrame.SETTINGS_MAX_FRAME_SIZE, connection.LocalSettings.MaxFrameSize }
                }
            };
            
            await connection.Writer!.WriteFrameAsync(ourSettings.ToFrame());
            Console.WriteLine("[HTTP/2 Preface] ✅ Sent our SETTINGS frame");

            // ارسال SETTINGS ACK برای client settings
            await connection.Writer.WriteFrameAsync(new SettingsFrame().ToFrame(ack: true));
            Console.WriteLine("[HTTP/2 Preface] ✅ Sent SETTINGS ACK");

            // منتظر SETTINGS ACK از client
            Console.WriteLine("[HTTP/2 Preface] Waiting for SETTINGS ACK...");
            var ackFrame = await connection.Reader.ReadFrameAsync();
            
            if (ackFrame == null)
            {
                Console.WriteLine("[HTTP/2 Preface] ❌ No SETTINGS ACK received");
                throw new InvalidOperationException("Connection closed before SETTINGS ACK");
            }

            // if (ackFrame.Type != Http2FrameType.Settings || !ackFrame.Ack)
            // {
            //     Console.WriteLine($"[HTTP/2 Preface] ❌ Expected SETTINGS ACK, got {ackFrame.Type} (ACK={ackFrame.Ack})");
            //     throw new InvalidOperationException("Expected SETTINGS ACK from client");
            // }
            // Console.WriteLine("[HTTP/2 Preface] ✅ Received SETTINGS ACK");

            connection.HandshakeComplete = true;
            Console.WriteLine("[HTTP/2 Preface] 🎉 HTTP/2 handshake complete!");
            
            // ذخیره connection در context
            ctx.Items["http2"] = connection;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HTTP/2 Preface] ❌ Handshake failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[HTTP/2 Preface]    Inner: {ex.InnerException.Message}");
            }
            throw new InvalidOperationException("HTTP/2 handshake failed", ex);
        }

        await next(ctx);
    }

    private static bool IsPrefaceValid(byte[] preface)
    {
        if (preface.Length != ClientPreface.Length)
            return false;

        for (int i = 0; i < ClientPreface.Length; i++)
        {
            if (preface[i] != ClientPreface[i])
                return false;
        }

        return true;
    }
}