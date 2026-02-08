using System.Buffers.Binary;

namespace WRM.HTTP.HTTP2.Frames;

public class Http2FrameWriter
{
    private readonly System.IO.Stream _stream;
    private readonly byte[] _headerBuffer = new byte[9];

    public Http2FrameWriter(System.IO.Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// نوشتن یک فریم کامل به stream
    /// </summary>
    public async Task WriteFrameAsync(Http2Frame frame, CancellationToken cancellationToken = default)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));

        // بررسی محدودیت‌ها
        if (frame.Length > 0xFFFFFF) // 2^24 - 1
            throw new ArgumentException("Frame length exceeds maximum allowed size (16MB)");

        if (frame.Payload != null && frame.Payload.Length != frame.Length)
            throw new ArgumentException("Payload length doesn't match frame length field");

        // ساخت header (9 بایت)
        // Length (3 bytes, big-endian)
        _headerBuffer[0] = (byte)(frame.Length >> 16);
        _headerBuffer[1] = (byte)(frame.Length >> 8);
        _headerBuffer[2] = (byte)frame.Length;

        // Type (1 byte)
        _headerBuffer[3] = (byte)frame.Type;

        // Flags (1 byte)
        _headerBuffer[4] = frame.Flags;

        // Stream ID (4 bytes, big-endian, بیت اول همیشه 0)
        BinaryPrimitives.WriteInt32BigEndian(_headerBuffer.AsSpan(5), frame.StreamId & 0x7FFFFFFF);

        // نوشتن header
        await _stream.WriteAsync(_headerBuffer, 0, 9, cancellationToken);

        // نوشتن payload (اگه وجود داشته باشه)
        if (frame.Payload != null && frame.Payload.Length > 0)
        {
            await _stream.WriteAsync(frame.Payload, 0, frame.Payload.Length, cancellationToken);
        }

        await _stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// نوشتن connection preface (باید اولین چیزی باشه که client می‌فرسته)
    /// </summary>
    public async Task WriteClientPrefaceAsync(CancellationToken cancellationToken = default)
    {
        // RFC 7540 Section 3.5: PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n
        byte[] preface =
        [
            0x50, 0x52, 0x49, 0x20, 0x2a, 0x20, 0x48, 0x54, // PRI * HT
            0x54, 0x50, 0x2f, 0x32, 0x2e, 0x30, 0x0d, 0x0a, // TP/2.0..
            0x0d, 0x0a, 0x53, 0x4d, 0x0d, 0x0a, 0x0d, 0x0a  // ..SM....
        ];

        await _stream.WriteAsync(preface, 0, preface.Length, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }
}
