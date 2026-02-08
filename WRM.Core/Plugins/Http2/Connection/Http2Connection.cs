using System.Collections.Concurrent;
using System.IO;
using WRM.Core.Plugins.Http2.Frames;
using WRM.Core.Plugins.Http2.Stream;
using WRM.Core.Plugins.Http2.HPACK;

namespace WRM.Core.Plugins.Http2.Connection;

/// <summary>
/// نمایش یک connection کامل HTTP/2
/// </summary>
public class Http2Connection
{
    public ConcurrentDictionary<int, Http2Stream> Streams { get; } = new();
    public Http2Settings LocalSettings { get; set; } = new();
    public Http2Settings RemoteSettings { get; set; } = new();
    
    public Http2FrameReader? Reader { get; set; }
    public Http2FrameWriter? Writer { get; set; }
    
    // HPACK encoder/decoder برای فشرده‌سازی headers
    public HpackEncoder Encoder { get; private set; } = new();
    public HpackDecoder Decoder { get; private set; } = new();
    
    private int _nextStreamId = 1; // Stream IDs باید فرد باشن برای client-initiated
    
    /// <summary>
    /// آیا این connection از طرف سرور هست؟
    /// </summary>
    public bool IsServer { get; set; }
    
    /// <summary>
    /// آیا handshake اولیه کامل شده؟
    /// </summary>
    public bool HandshakeComplete { get; set; }

    public void Initialize(System.IO.Stream networkStream, bool isServer)
    {
        IsServer = isServer;
        Reader = new Http2FrameReader(networkStream);
        Writer = new Http2FrameWriter(networkStream);
        
        // اگه server هستیم، stream IDs باید زوج باشن
        _nextStreamId = isServer ? 2 : 1;
    }

    /// <summary>
    /// گرفتن stream ID بعدی
    /// </summary>
    public int GetNextStreamId()
    {
        int id = _nextStreamId;
        _nextStreamId += 2; // همیشه 2 تا اضافه می‌کنیم تا فرد/زوج بودن حفظ بشه
        return id;
    }

    /// <summary>
    /// ساخت یا گرفتن یک stream
    /// </summary>
    public Http2Stream GetOrCreateStream(int streamId)
    {
        return Streams.GetOrAdd(streamId, id => new Http2Stream
        {
            Id = id,
            State = Http2StreamState.Idle
        });
    }

    /// <summary>
    /// حذف یک stream
    /// </summary>
    public bool RemoveStream(int streamId)
    {
        return Streams.TryRemove(streamId, out _);
    }
}