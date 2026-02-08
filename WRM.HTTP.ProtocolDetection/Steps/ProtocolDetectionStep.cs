using System.Text;
using WRM.Interface;
using WRM.WrappedConnection.Streams;

namespace WRM.HTTP.ProtocolDetection.Steps;

public sealed class ProtocolDetectionStep : IPipelineStep
{
    private const int PeekSize = 32;

    public async Task InvokeAsync(
        NetworkContext ctx,
        Func<NetworkContext, Task> next)
    {
        var stream = ctx.Connection.Stream;

        var buffer = new byte[PeekSize];
        int read = await stream.ReadAsync(
            buffer, 0, buffer.Length, ctx.Cancellation);

        var protocol = Detect(buffer, read);

        ctx.Items["HTTP_PROTOCOL"] = protocol;

        ctx.Loger?.LogAsync(this, ILoger.LogLevel.Info, $"  Detected Protocol {protocol}");
        ctx.Items["original-stream"] = stream;
        ctx.Connection = new WrappedConnection.Connections.WrappedConnection(
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
            var text = Encoding.ASCII.GetString(buf);
            return text.StartsWith(s, StringComparison.OrdinalIgnoreCase);
        }
    }
}