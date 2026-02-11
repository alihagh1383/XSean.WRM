namespace WRM.Core.Plugins.Http.Abstraction;

public class HttpResponse
{
    public int StatusCode = 200;
    public string? ReasonPhrase;
    public List<KeyValuePair<string, string>> Hreaders = [];
}