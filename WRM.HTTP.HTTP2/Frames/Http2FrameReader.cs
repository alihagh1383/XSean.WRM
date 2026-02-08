using System.Buffers.Binary;

namespace WRM.HTTP.HTTP2.Frames;

public class Http2FrameReader
{
    private readonly System.IO.Stream _stream;
    private readonly byte[] _headerBuffer = new byte[9]; // HTTP/2 frame header همیشه 9 بایته

    public Http2FrameReader(System.IO.Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// خواندن یک فریم کامل از stream
    /// </summary>
    public async Task<Http2Frame?> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        // اول 9 بایت header رو می‌خونیم
        int bytesRead = await ReadExactAsync(_headerBuffer, 0, 9, cancellationToken);
        
        if (bytesRead == 0)
        {
            // Connection بسته شده
            return null;
        }

        if (bytesRead < 9)
        {
            throw new IOException("Incomplete frame header received");
        }

        // Parse frame header (RFC 7540 Section 4.1)
        // +-----------------------------------------------+
        // |                 Length (24)                   |
        // +---------------+---------------+---------------+
        // |   Type (8)    |   Flags (8)   |
        // +-+-------------+---------------+-------------------------------+
        // |R|                 Stream Identifier (31)                      |
        // +=+=============================================================+
        
        // Length: 3 بایت اول (big-endian)
        uint length = (uint)(_headerBuffer[0] << 16 | _headerBuffer[1] << 8 | _headerBuffer[2]);
        
        // Type: بایت چهارم
        var type = (Http2FrameType)_headerBuffer[3];
        
        // Flags: بایت پنجم
        byte flags = _headerBuffer[4];
        
        // Stream ID: 4 بایت آخر (big-endian) با حذف بیت R
        int streamId = BinaryPrimitives.ReadInt32BigEndian(_headerBuffer.AsSpan(5)) & 0x7FFFFFFF;

        // حالا payload رو می‌خونیم
        byte[] payload = new byte[length];
        if (length > 0)
        {
            bytesRead = await ReadExactAsync(payload, 0, (int)length, cancellationToken);
            if (bytesRead < length)
            {
                throw new IOException($"Incomplete frame payload. Expected {length} bytes, got {bytesRead}");
            }
        }

        return new Http2Frame
        {
            Length = length,
            Type = type,
            Flags = flags,
            StreamId = streamId,
            Payload = payload
        };
    }

    /// <summary>
    /// خواندن دقیق تعداد مشخصی بایت (یا EOF)
    /// </summary>
    private async Task<int> ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        
        while (totalRead < count)
        {
            int read = await _stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken);
            
            if (read == 0)
            {
                // EOF - اگه هیچی نخوندیم، 0 برمی‌گردونیم، وگرنه exception
                return totalRead == 0 ? 0 : totalRead;
            }
            
            totalRead += read;
        }
        
        return totalRead;
    }
}
