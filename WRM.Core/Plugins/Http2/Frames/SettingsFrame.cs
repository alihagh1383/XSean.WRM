using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace WRM.Core.Plugins.Http2.Frames;

/// <summary>
/// SETTINGS frame - برای تبادل تنظیمات بین client و server
/// </summary>
public class SettingsFrame
{
    // شناسه‌های تنظیمات طبق RFC 7540
    public const ushort SETTINGS_HEADER_TABLE_SIZE = 0x1;
    public const ushort SETTINGS_ENABLE_PUSH = 0x2;
    public const ushort SETTINGS_MAX_CONCURRENT_STREAMS = 0x3;
    public const ushort SETTINGS_INITIAL_WINDOW_SIZE = 0x4;
    public const ushort SETTINGS_MAX_FRAME_SIZE = 0x5;
    public const ushort SETTINGS_MAX_HEADER_LIST_SIZE = 0x6;

    public Dictionary<ushort, uint> Parameters { get; set; } = new();

    /// <summary>
    /// ساخت SETTINGS frame از یک Http2Frame
    /// </summary>
    public static SettingsFrame Parse(Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.Settings)
            throw new ArgumentException("Frame is not a SETTINGS frame");

        if (frame.StreamId != 0)
            throw new InvalidOperationException("SETTINGS frame must have stream ID 0");

        var settings = new SettingsFrame();

        // اگه ACK باشه، نباید payload داشته باشه
        if (frame.Ack)
        {
            if (frame.Length != 0)
                throw new InvalidOperationException("SETTINGS ACK must have zero length");
            return settings;
        }

        // هر setting یه جفت (identifier: 2 bytes, value: 4 bytes) هست
        if (frame.Length % 6 != 0)
            throw new InvalidOperationException("SETTINGS payload length must be multiple of 6");

        for (int i = 0; i < frame.Payload.Length; i += 6)
        {
            ushort id = BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(i, 2));
            uint value = BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(i + 2, 4));
            
            settings.Parameters[id] = value;
        }

        return settings;
    }

    /// <summary>
    /// تبدیل به Http2Frame
    /// </summary>
    public Http2Frame ToFrame(bool ack = false)
    {
        byte[] payload;
        
        if (ack)
        {
            payload = Array.Empty<byte>();
        }
        else
        {
            payload = new byte[Parameters.Count * 6];
            int offset = 0;
            
            foreach (var kvp in Parameters)
            {
                BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(offset, 2), kvp.Key);
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(offset + 2, 4), kvp.Value);
                offset += 6;
            }
        }

        return new Http2Frame
        {
            Type = Http2FrameType.Settings,
            StreamId = 0, // SETTINGS همیشه stream 0
            Flags = (byte)(ack ? 0x1 : 0x0),
            Length = (uint)payload.Length,
            Payload = payload
        };
    }

    /// <summary>
    /// اعمال این settings به یک Http2Settings
    /// </summary>
    public void ApplyTo(Connection.Http2Settings settings)
    {
        foreach (var kvp in Parameters)
        {
            switch (kvp.Key)
            {
                case SETTINGS_HEADER_TABLE_SIZE:
                    settings.HeaderTableSize = kvp.Value;
                    break;
                    
                case SETTINGS_ENABLE_PUSH:
                    if (kvp.Value > 1)
                        throw new InvalidOperationException("ENABLE_PUSH must be 0 or 1");
                    settings.EnablePush = kvp.Value == 1;
                    break;
                    
                case SETTINGS_MAX_CONCURRENT_STREAMS:
                    settings.MaxConcurrentStreams = kvp.Value;
                    break;
                    
                case SETTINGS_INITIAL_WINDOW_SIZE:
                    if (kvp.Value > 0x7FFFFFFF)
                        throw new InvalidOperationException("INITIAL_WINDOW_SIZE exceeds maximum");
                    settings.InitialWindowSize = kvp.Value;
                    break;
                    
                case SETTINGS_MAX_FRAME_SIZE:
                    if (kvp.Value < 16384 || kvp.Value > 16777215)
                        throw new InvalidOperationException("MAX_FRAME_SIZE out of valid range");
                    settings.MaxFrameSize = kvp.Value;
                    break;
                    
                case SETTINGS_MAX_HEADER_LIST_SIZE:
                    settings.MaxHeaderListSize = kvp.Value;
                    break;
                    
                // شناسه‌های ناشناخته رو ignore می‌کنیم (طبق spec)
            }
        }
    }

    public override string ToString()
    {
        return $"SETTINGS: {string.Join(", ", Parameters.Select(p => $"{p.Key}={p.Value}"))}";
    }
}
