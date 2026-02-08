using System.Buffers.Binary;
using System.Text;

namespace WRM.Core.Plugins.Http2.Frames;

/// <summary>
/// GOAWAY frame - برای بستن graceful یک connection
/// </summary>
public class GoAwayFrame
{
    public int LastStreamId { get; set; }
    public uint ErrorCode { get; set; }
    public byte[] DebugData { get; set; } = Array.Empty<byte>();

    // Error codes طبق RFC 7540
    public const uint NO_ERROR = 0x0;
    public const uint PROTOCOL_ERROR = 0x1;
    public const uint INTERNAL_ERROR = 0x2;
    public const uint FLOW_CONTROL_ERROR = 0x3;
    public const uint SETTINGS_TIMEOUT = 0x4;
    public const uint STREAM_CLOSED = 0x5;
    public const uint FRAME_SIZE_ERROR = 0x6;
    public const uint REFUSED_STREAM = 0x7;
    public const uint CANCEL = 0x8;
    public const uint COMPRESSION_ERROR = 0x9;
    public const uint CONNECT_ERROR = 0xa;
    public const uint ENHANCE_YOUR_CALM = 0xb;
    public const uint INADEQUATE_SECURITY = 0xc;
    public const uint HTTP_1_1_REQUIRED = 0xd;

    /// <summary>
    /// Parse کردن GOAWAY frame
    /// </summary>
    public static GoAwayFrame Parse(Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.GoAway)
            throw new ArgumentException("Frame is not a GOAWAY frame");

        if (frame.StreamId != 0)
            throw new InvalidOperationException("GOAWAY frame must have stream ID 0");

        if (frame.Length < 8)
            throw new InvalidOperationException("GOAWAY frame must be at least 8 bytes");

        int lastStreamId = BinaryPrimitives.ReadInt32BigEndian(frame.Payload.AsSpan(0, 4));
        lastStreamId &= 0x7FFFFFFF; // حذف reserved bit

        uint errorCode = BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(4, 4));

        byte[] debugData = Array.Empty<byte>();
        if (frame.Length > 8)
        {
            debugData = new byte[frame.Length - 8];
            Array.Copy(frame.Payload, 8, debugData, 0, debugData.Length);
        }

        return new GoAwayFrame
        {
            LastStreamId = lastStreamId,
            ErrorCode = errorCode,
            DebugData = debugData
        };
    }

    /// <summary>
    /// تبدیل به Http2Frame
    /// </summary>
    public Http2Frame ToFrame()
    {
        byte[] payload = new byte[8 + DebugData.Length];

        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0, 4), LastStreamId & 0x7FFFFFFF);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), ErrorCode);

        if (DebugData.Length > 0)
        {
            Array.Copy(DebugData, 0, payload, 8, DebugData.Length);
        }

        return new Http2Frame
        {
            Type = Http2FrameType.GoAway,
            StreamId = 0,
            Flags = 0,
            Length = (uint)payload.Length,
            Payload = payload
        };
    }

    public string GetDebugMessage()
    {
        if (DebugData.Length == 0)
            return string.Empty;

        return Encoding.UTF8.GetString(DebugData);
    }

    public override string ToString()
    {
        return $"GOAWAY: LastStreamId={LastStreamId}, ErrorCode={ErrorCode}, Debug={GetDebugMessage()}";
    }
}
