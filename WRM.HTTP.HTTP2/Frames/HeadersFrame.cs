using System.Buffers.Binary;
using WRM.HTTP.HTTP2.HPACK;

namespace WRM.HTTP.HTTP2.Frames;

public class HeadersFrame
{
    public List<(string Name, string Value)> Headers { get; set; } = new();
    public bool EndStream { get; set; }
    public bool EndHeaders { get; set; }
    public byte PadLength { get; set; } = 0;
    
    // Priority information (اختیاری)
    public bool HasPriority { get; set; }
    public int StreamDependency { get; set; }
    public byte Weight { get; set; }
    public bool Exclusive { get; set; }

    /// <summary>
    /// Parse کردن HEADERS frame
    /// </summary>
    public static HeadersFrame Parse(Http2Frame frame, HpackDecoder decoder)
    {
        if (frame.Type != Http2FrameType.Headers)
            throw new ArgumentException("Frame is not a HEADERS frame");

        if (frame.StreamId == 0)
            throw new InvalidOperationException("HEADERS frame must not use stream 0");

        var headersFrame = new HeadersFrame
        {
            EndStream = frame.EndStream,
            EndHeaders = frame.EndHeaders,
            HasPriority = frame.Priority
        };

        int offset = 0;

        // اگه PADDED flag داشته باشه
        if (frame.Padded)
        {
            if (frame.Payload.Length < 1)
                throw new InvalidOperationException("PADDED frame must have at least 1 byte");

            headersFrame.PadLength = frame.Payload[0];
            offset = 1;
        }

        // اگه PRIORITY flag داشته باشه
        if (frame.Priority)
        {
            if (frame.Payload.Length - offset < 5)
                throw new InvalidOperationException("PRIORITY data incomplete");

            // Stream Dependency (4 bytes)
            int dependency = BinaryPrimitives.ReadInt32BigEndian(frame.Payload.AsSpan(offset, 4));
            headersFrame.Exclusive = (dependency & 0x80000000) != 0;
            headersFrame.StreamDependency = dependency & 0x7FFFFFFF;
            
            // Weight (1 byte)
            headersFrame.Weight = frame.Payload[offset + 4];
            offset += 5;
        }

        // محاسبه طول header block (بدون padding)
        int headerBlockLength = frame.Payload.Length - offset - headersFrame.PadLength;
        if (headerBlockLength < 0)
            throw new InvalidOperationException("Invalid padding length");

        // Decode headers با HPACK
        if (headerBlockLength > 0)
        {
            var headerBlock = frame.Payload.AsSpan(offset, headerBlockLength);
            headersFrame.Headers = decoder.Decode(headerBlock);
        }

        return headersFrame;
    }

    /// <summary>
    /// تبدیل به Http2Frame
    /// </summary>
    public Http2Frame ToFrame(int streamId, HpackEncoder encoder)
    {
        if (streamId == 0)
            throw new ArgumentException("HEADERS frame must not use stream 0");

        // Encode headers با HPACK
        byte[] headerBlock = encoder.Encode(Headers);

        // محاسبه اندازه payload
        int payloadSize = headerBlock.Length;
        bool padded = PadLength > 0;
        
        if (padded)
            payloadSize += 1 + PadLength; // pad length field + padding
            
        if (HasPriority)
            payloadSize += 5; // priority data

        byte[] payload = new byte[payloadSize];
        int offset = 0;

        // نوشتن pad length اگه لازم باشه
        if (padded)
        {
            payload[0] = PadLength;
            offset = 1;
        }

        // نوشتن priority اگه لازم باشه
        if (HasPriority)
        {
            int dependency = StreamDependency;
            if (Exclusive)
                dependency |= unchecked((int)0x80000000);

            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, 4), dependency);
            payload[offset + 4] = Weight;
            offset += 5;
        }

        // کپی header block
        Array.Copy(headerBlock, 0, payload, offset, headerBlock.Length);

        // Padding در انتها قرار می‌گیره (بایت‌های صفر)

        byte flags = 0;
        if (EndStream) flags |= 0x01;
        if (EndHeaders) flags |= 0x04;
        if (padded) flags |= 0x08;
        if (HasPriority) flags |= 0x20;

        return new Http2Frame
        {
            Type = Http2FrameType.Headers,
            StreamId = streamId,
            Flags = flags,
            Length = (uint)payload.Length,
            Payload = payload
        };
    }

    public override string ToString()
    {
        return $"HEADERS: {Headers.Count} headers, EndStream={EndStream}, EndHeaders={EndHeaders}";
    }
}
