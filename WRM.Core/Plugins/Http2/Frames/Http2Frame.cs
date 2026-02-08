namespace WRM.Core.Plugins.Http2.Frames;

/// <summary>
/// نمایش یک فریم HTTP/2 طبق RFC 7540
/// </summary>
public class Http2Frame
{
    public uint Length { get; set; }
    public Http2FrameType Type { get; set; }
    public byte Flags { get; set; }
    public int StreamId { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    // Flag helpers برای انواع مختلف فریم‌ها
    
    /// <summary>
    /// END_STREAM flag (0x1) - برای DATA و HEADERS
    /// </summary>
    public bool EndStream
    {
        get => (Flags & 0x1) != 0;
        set => Flags = value ? (byte)(Flags | 0x1) : (byte)(Flags & ~0x1);
    }

    /// <summary>
    /// END_HEADERS flag (0x4) - برای HEADERS
    /// </summary>
    public bool EndHeaders
    {
        get => (Flags & 0x4) != 0;
        set => Flags = value ? (byte)(Flags | 0x4) : (byte)(Flags & ~0x4);
    }

    /// <summary>
    /// PADDED flag (0x8) - برای DATA و HEADERS
    /// </summary>
    public bool Padded
    {
        get => (Flags & 0x8) != 0;
        set => Flags = value ? (byte)(Flags | 0x8) : (byte)(Flags & ~0x8);
    }

    /// <summary>
    /// PRIORITY flag (0x20) - برای HEADERS
    /// </summary>
    public bool Priority
    {
        get => (Flags & 0x20) != 0;
        set => Flags = value ? (byte)(Flags | 0x20) : (byte)(Flags & ~0x20);
    }

    /// <summary>
    /// ACK flag (0x1) - برای SETTINGS و PING
    /// </summary>
    public bool Ack
    {
        get => (Flags & 0x1) != 0;
        set => Flags = value ? (byte)(Flags | 0x1) : (byte)(Flags & ~0x1);
    }

    public override string ToString()
    {
        return $"[{Type}] Stream={StreamId}, Length={Length}, Flags=0x{Flags:X2}";
    }
}
