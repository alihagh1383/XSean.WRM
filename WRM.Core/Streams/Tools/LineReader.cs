using System.Text;

namespace WRM.Core.Streams.Tools;

public sealed class LineReader
{
    private readonly Stream _stream;

    public LineReader(Stream stream)
    {
        _stream = stream;
    }

    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var buffer = new List<byte>(128);

        while (true)
        {
            int b = _stream.ReadByte();
            if (b == -1)
                return null;

            if (b == '\n')
                break;

            if (b != '\r')
                buffer.Add((byte)b);
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }
}
