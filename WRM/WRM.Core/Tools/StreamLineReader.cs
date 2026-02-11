using System.Text;

namespace WRM.Core.Tools;

public class StreamLineReader(Stream stream)
{
    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var buffer = new List<byte>(128);
        var buf = new byte[1];
        while (true)
        {
            var b = await stream.ReadAsync(buf, ct);
            if (b == 0)
                return (null);
            if (buf[0] == '\n')
                break;

            if (buf[0] != '\r')
                buffer.Add(buf[0]);
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }
}