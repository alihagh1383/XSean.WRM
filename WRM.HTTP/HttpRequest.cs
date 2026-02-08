namespace WRM.HTTP;

public sealed class HttpRequest
{
    public string Method { get; init; } = "";
    public string Path { get; init; } = "";
    public string Version { get; init; } = "";
    public ICollection<KeyValuePair<string, string>> Headers { get; } = [];

    public bool IsConnect => Method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase);
    public System.IO.Stream? Body;
}