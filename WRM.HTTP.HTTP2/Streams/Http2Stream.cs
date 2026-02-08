namespace WRM.HTTP.HTTP2.Streams;

public class Http2Stream: HttpContext
{
    public int Id;
    public Http2StreamState State;

    public bool HeadersReceived { get; set; }
    public bool EndStreamReceived { get; set; }
    public bool ResponseSent { get; set; }
}