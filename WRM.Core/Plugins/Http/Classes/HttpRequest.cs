namespace WRM.Core.Plugins.Http.Classes;

public sealed class HttpRequest
{
    public string Method { get; init; } = "";
    public string Path { get; init; } = "";
    public string Version { get; init; } = "";
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool IsConnect => Method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase);
}
