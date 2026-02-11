using System.Net.Sockets;
using System.Text;
using WRM.Core.Interfaces;
using WRM.Core.Plugins.ProtocolDetection.Enums;
using WRM.Core.Streams;

namespace WRM.Core.Plugins.ProtocolDetection.Steps;

public class ProtocolDetectionStep : IPluginStep
{
    private const int PeekSize = 25;

    public async Task InvokeAsync(WRMContext context, Func<WRMContext, Task> next)
    {
        var protocol = Protocols.Unknown;
        if (context.Items.TryGetValue(Names.TcpStream, out var objectStream) && objectStream is Stream stream)
        {
            var buffer = new byte[PeekSize];
            var read = await stream.ReadAsync(buffer.AsMemory(0, PeekSize), context.CancellationToken);
            var buf = buffer[..read];
            if (read >= 24)
            {
                ReadOnlySpan<byte> preface = buf.AsSpan(0, 24);
                if (preface.SequenceEqual("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8)) protocol = Protocols.Http2;
            }

            var text = Encoding.ASCII.GetString(buf);
            context.Loger?.LogTest(this, $"Buffer Text = {text.Replace("\n", "\\n").Replace("\r", "\\r")}");
            if (StartsWith(text, "GET") ||
                StartsWith(text, "POST") ||
                StartsWith(text, "HEAD") ||
                StartsWith(text, "CONN"))
                protocol = Protocols.Http1;

            static bool StartsWith(string text, string s)
            {
                return text.StartsWith(s, StringComparison.OrdinalIgnoreCase);
            }

            context.Items[Names.TcpStream] = new BufferedPeekStream(stream, buffer[..read]);
        }

        context.Loger?.LogInfo(this, $"Detected Protocol {protocol}");
        context.Items[Names.HttpProtocol] = protocol;

        await next(context);
    }

    private static Protocols Detect(byte[] buf, int len)
    {
        if (len >= 24)
        {
            // HTTP/2 connection preface (binary-safe)
            ReadOnlySpan<byte> preface = buf.AsSpan(0, 24);

            if (preface.SequenceEqual("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8))
            {
                return Protocols.Http2;
            }
        }

        var text = Encoding.ASCII.GetString(buf);

        if (StartsWith(text, "GET") ||
            StartsWith(text, "POST") ||
            StartsWith(text, "HEAD") ||
            StartsWith(text, "CONN"))
        {
            return Protocols.Http1;
        }

        return Protocols.Unknown;

        static bool StartsWith(string text, string s)
        {
            return text.StartsWith(s, StringComparison.OrdinalIgnoreCase);
        }
    }
}