namespace WRM.HTTP.HTTP2.Frames;

public enum Http2FrameType
{
    Data = 0,
    Headers = 1,
    Priority = 2,
    RstStream = 3,
    Settings = 4,
    Ping = 6,
    GoAway = 7,
    WindowUpdate = 8
}
