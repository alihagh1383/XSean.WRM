using System.Buffers.Binary;

namespace WRM.HTTP.HTTP2.Frames;

public class RstStreamFrame
{
    public uint ErrorCode { get; set; }

    // Error codes (مشابه GoAwayFrame)
    public const uint NO_ERROR = 0x0;
    public const uint PROTOCOL_ERROR = 0x1;
    public const uint INTERNAL_ERROR = 0x2;
    public const uint FLOW_CONTROL_ERROR = 0x3;
    public const uint SETTINGS_TIMEOUT = 0x4;
    public const uint STREAM_CLOSED = 0x5;
    public const uint FRAME_SIZE_ERROR = 0x6;
    public const uint REFUSED_STREAM = 0x7;
    public const uint CANCEL = 0x8;

    /// <summary>
    /// Parse کردن RST_STREAM frame
    /// </summary>
    public static RstStreamFrame Parse(Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.RstStream)
            throw new ArgumentException("Frame is not a RST_STREAM frame");

        if (frame.StreamId == 0)
            throw new InvalidOperationException("RST_STREAM frame must not use stream 0");

        if (frame.Length != 4)
            throw new InvalidOperationException("RST_STREAM frame must be exactly 4 bytes");

        uint errorCode = BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(0, 4));

        return new RstStreamFrame
        {
            ErrorCode = errorCode
        };
    }

    /// <summary>
    /// تبدیل به Http2Frame
    /// </summary>
    public Http2Frame ToFrame(int streamId)
    {
        if (streamId == 0)
            throw new ArgumentException("RST_STREAM frame must not use stream 0");

        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(payload, ErrorCode);

        return new Http2Frame
        {
            Type = Http2FrameType.RstStream,
            StreamId = streamId,
            Flags = 0,
            Length = 4,
            Payload = payload
        };
    }

    public override string ToString()
    {
        return $"RST_STREAM: ErrorCode={ErrorCode}";
    }
}
