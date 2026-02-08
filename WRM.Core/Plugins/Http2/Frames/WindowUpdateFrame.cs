using System.Buffers.Binary;

namespace WRM.Core.Plugins.Http2.Frames;

/// <summary>
/// WINDOW_UPDATE frame - برای مدیریت flow control
/// </summary>
public class WindowUpdateFrame
{
    public uint WindowSizeIncrement { get; set; }

    /// <summary>
    /// Parse کردن WINDOW_UPDATE frame
    /// </summary>
    public static WindowUpdateFrame Parse(Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.WindowUpdate)
            throw new ArgumentException("Frame is not a WINDOW_UPDATE frame");

        if (frame.Length != 4)
            throw new InvalidOperationException("WINDOW_UPDATE frame must be exactly 4 bytes");

        uint increment = BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(0, 4));
        increment &= 0x7FFFFFFF; // حذف reserved bit

        if (increment == 0)
            throw new InvalidOperationException("WINDOW_UPDATE increment must not be zero");

        return new WindowUpdateFrame
        {
            WindowSizeIncrement = increment
        };
    }

    /// <summary>
    /// تبدیل به Http2Frame
    /// </summary>
    public Http2Frame ToFrame(int streamId)
    {
        if (WindowSizeIncrement == 0)
            throw new InvalidOperationException("WINDOW_UPDATE increment must not be zero");

        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(payload, WindowSizeIncrement & 0x7FFFFFFF);

        return new Http2Frame
        {
            Type = Http2FrameType.WindowUpdate,
            StreamId = streamId,
            Flags = 0,
            Length = 4,
            Payload = payload
        };
    }

    public override string ToString()
    {
        return $"WINDOW_UPDATE: Increment={WindowSizeIncrement}";
    }
}
