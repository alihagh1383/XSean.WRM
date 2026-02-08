namespace WRM.HTTP;

public class HttpResponse
{
    public int StatusCode { get; set; } = 200;
    public string StatusDescription { get; set; } = "OK";

    public ICollection<KeyValuePair<string, string>> Headers { get; } = [];

    public System.IO.Stream? Body;
}