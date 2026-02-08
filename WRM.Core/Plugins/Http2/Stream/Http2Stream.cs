using WRM.Core.Plugins.Http.Classes;

namespace WRM.Core.Plugins.Http2.Stream;

public class Http2Stream : HttpContext
{
    public int Id;
    public Http2StreamState State;

    public bool HeadersReceived { get; set; }
    public bool EndStreamReceived { get; set; }
    public bool ResponseSent { get; set; }
}