namespace WRM.HTTP.HTTP2.Connection;

public sealed class Http2Settings
{
    public uint HeaderTableSize { get; set; } = 4096;
    public bool EnablePush { get; set; } = true;
    public uint MaxConcurrentStreams { get; set; } = 100; // مقدار پیشنهادی
    public uint InitialWindowSize { get; set; } = 65535;
    public uint MaxFrameSize { get; set; } = 16384;
    public uint MaxHeaderListSize { get; set; } = uint.MaxValue;
  
}