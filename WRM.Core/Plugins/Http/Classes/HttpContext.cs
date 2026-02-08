namespace WRM.Core.Plugins.Http.Classes;

public class HttpContext
{
    public HttpRequest Request { get; set; } = null!;
    public System.IO.Stream Body;
}
