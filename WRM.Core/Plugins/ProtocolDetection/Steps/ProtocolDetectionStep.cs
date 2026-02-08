using System;
using System.Text;
using System.Threading.Tasks;
using WRM.Core.Connections;
using WRM.Core.Interface;
using WRM.Core.Streams;

namespace WRM.Core.Plugins.ProtocolDetection.Steps;

public sealed class ProtocolDetectionStep : IPipelineStep
{
    private const int PeekSize = 32;

    public async Task InvokeAsync(
        NetworkContext ctx,
        Func<NetworkContext,Task> next)
    {
        var stream = ctx.Connection.Stream;

        var buffer = new byte[PeekSize];
        int read = await stream.ReadAsync(
            buffer, 0, buffer.Length, ctx.Cancellation);

        var protocol = Detect(buffer, read);

        ctx.Items["protocol"] = protocol;

        Console.WriteLine($"  Detected Protocol {protocol}");
        ctx.Items["original-stream"] = stream;
        ctx.Connection = new WrappedConnection(
            ctx.Connection,
            new BufferedPeekStream(stream, buffer[..read])
        );

        await next(ctx);
    }

    private static DetectedProtocol Detect(byte[] buf, int len)
    {
        if (len >= 24)
        {
            // HTTP/2 connection preface (binary-safe)
            ReadOnlySpan<byte> preface = buf.AsSpan(0, 24);

            if (preface.SequenceEqual(
                    "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8))
            {
                return DetectedProtocol.Http2;
            }
        }

        if (len >= 3 &&
            buf[0] == 0x16 && // Handshake
            buf[1] == 0x03 && // TLS major
            buf[2] <= 0x04)
            return DetectedProtocol.Tls;

        if (len >= 4)
        {
            var span = buf.AsSpan(0, len);

            if (StartsWith(span, "GET ") ||
                StartsWith(span, "POST") ||
                StartsWith(span, "HEAD") ||
                StartsWith(span, "CONN"))
            {
                return DetectedProtocol.Http1;
            }
        }

        return DetectedProtocol.Unknown;

        static bool StartsWith(ReadOnlySpan<byte> buf, string s)
        {
            var bytes = Encoding.ASCII.GetBytes(s);
            return buf.Length >= bytes.Length &&
                   buf[..bytes.Length].SequenceEqual(bytes);
        }
    }
}