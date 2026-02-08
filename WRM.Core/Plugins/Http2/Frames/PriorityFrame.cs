using System.Buffers.Binary;

namespace WRM.Core.Plugins.Http2.Frames;

/// <summary>
/// PRIORITY frame - برای تنظیم اولویت stream
/// </summary>
public class PriorityFrame
{
    public int StreamDependency { get; set; }
    public byte Weight { get; set; }
    public bool Exclusive { get; set; }

    /// <summary>
    /// Parse کردن PRIORITY frame
    /// </summary>
    public static PriorityFrame Parse(Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.Priority)
            throw new ArgumentException("Frame is not a PRIORITY frame");

        if (frame.StreamId == 0)
            throw new InvalidOperationException("PRIORITY frame must not use stream 0");

        if (frame.Length != 5)
            throw new InvalidOperationException("PRIORITY frame must be exactly 5 bytes");

        int dependency = BinaryPrimitives.ReadInt32BigEndian(frame.Payload.AsSpan(0, 4));
        bool exclusive = (dependency & 0x80000000) != 0;
        dependency &= 0x7FFFFFFF;

        byte weight = frame.Payload[4];

        return new PriorityFrame
        {
            StreamDependency = dependency,
            Weight = weight,
            Exclusive = exclusive
        };
    }

    /// <summary>
    /// تبدیل به Http2Frame
    /// </summary>
    public Http2Frame ToFrame(int streamId)
    {
        if (streamId == 0)
            throw new ArgumentException("PRIORITY frame must not use stream 0");

        byte[] payload = new byte[5];

        int dependency = StreamDependency;
        if (Exclusive)
            dependency |= unchecked((int)0x80000000);

        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0, 4), dependency);
        payload[4] = Weight;

        return new Http2Frame
        {
            Type = Http2FrameType.Priority,
            StreamId = streamId,
            Flags = 0,
            Length = 5,
            Payload = payload
        };
    }

    public override string ToString()
    {
        return $"PRIORITY: Dependency={StreamDependency}, Weight={Weight}, Exclusive={Exclusive}";
    }
}
