using System;
using System.IO;

namespace WRM.Core.Plugins.Http2.Frames;

/// <summary>
/// DATA frame - برای انتقال داده‌های request/response body
/// </summary>
public class DataFrame
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public byte PadLength { get; set; } = 0;
    public bool EndStream { get; set; }

    /// <summary>
    /// Parse کردن DATA frame از یک Http2Frame
    /// </summary>
    public static DataFrame Parse(Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.Data)
            throw new ArgumentException("Frame is not a DATA frame");

        if (frame.StreamId == 0)
            throw new InvalidOperationException("DATA frames must not use stream 0");

        var dataFrame = new DataFrame
        {
            EndStream = frame.EndStream
        };

        int offset = 0;
        
        // اگه PADDED flag داشته باشه، اولین بایت Pad Length هست
        if (frame.Padded)
        {
            if (frame.Payload.Length < 1)
                throw new InvalidOperationException("PADDED frame must have at least 1 byte");
                
            dataFrame.PadLength = frame.Payload[0];
            offset = 1;
            
            // بررسی که padding از اندازه payload بیشتر نباشه
            if (dataFrame.PadLength >= frame.Payload.Length - offset)
                throw new InvalidOperationException("Padding exceeds frame payload");
        }

        // استخراج data واقعی (بدون padding)
        int dataLength = frame.Payload.Length - offset - dataFrame.PadLength;
        dataFrame.Data = new byte[dataLength];
        
        if (dataLength > 0)
        {
            Array.Copy(frame.Payload, offset, dataFrame.Data, 0, dataLength);
        }

        return dataFrame;
    }

    /// <summary>
    /// تبدیل به Http2Frame
    /// </summary>
    public Http2Frame ToFrame(int streamId)
    {
        if (streamId == 0)
            throw new ArgumentException("DATA frames must not use stream 0");

        byte[] payload;
        bool padded = PadLength > 0;
        int offset = 0;

        if (padded)
        {
            // payload = [pad_length(1)] + [data] + [padding]
            payload = new byte[1 + Data.Length + PadLength];
            payload[0] = PadLength;
            offset = 1;
        }
        else
        {
            payload = new byte[Data.Length];
        }

        // کپی data
        if (Data.Length > 0)
        {
            Array.Copy(Data, 0, payload, offset, Data.Length);
        }

        // اگه padding داریم، بایت‌های آخر صفر می‌مونن (که default هستن)

        return new Http2Frame
        {
            Type = Http2FrameType.Data,
            StreamId = streamId,
            Flags = (byte)((EndStream ? 0x1 : 0) | (padded ? 0x8 : 0)),
            Length = (uint)payload.Length,
            Payload = payload
        };
    }

    public override string ToString()
    {
        return $"DATA: {Data.Length} bytes, EndStream={EndStream}, Padding={PadLength}";
    }
}
