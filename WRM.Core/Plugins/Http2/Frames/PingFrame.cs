namespace WRM.Core.Plugins.Http2.Frames;

/// <summary>
/// PING frame - برای چک کردن connection health
/// </summary>
public class PingFrame
{
    public byte[] OpaqueData { get; set; } = new byte[8]; // همیشه 8 بایت
    public bool Ack { get; set; }

    /// <summary>
    /// Parse کردن PING frame
    /// </summary>
    public static PingFrame Parse(Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.Ping)
            throw new ArgumentException("Frame is not a PING frame");

        if (frame.StreamId != 0)
            throw new InvalidOperationException("PING frame must have stream ID 0");

        if (frame.Length != 8)
            throw new InvalidOperationException("PING frame must be exactly 8 bytes");

        return new PingFrame
        {
            OpaqueData = frame.Payload,
            Ack = frame.Ack
        };
    }

    /// <summary>
    /// تبدیل به Http2Frame
    /// </summary>
    public Http2Frame ToFrame()
    {
        if (OpaqueData.Length != 8)
            throw new InvalidOperationException("PING opaque data must be exactly 8 bytes");

        return new Http2Frame
        {
            Type = Http2FrameType.Ping,
            StreamId = 0,
            Flags = (byte)(Ack ? 0x1 : 0),
            Length = 8,
            Payload = OpaqueData
        };
    }

    public override string ToString()
    {
        return $"PING: Ack={Ack}";
    }
}
